﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using ECommon.Dapper;
using ECommon.Utilities;
using ENode.Configurations;

namespace ENode.Infrastructure.Impl.Mysql
{
    public class MySqlLockService : ILockService
    {
        #region Private Variables

        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _lockKeySqlFormat;

        #endregion

        #region Constructors

        public MySqlLockService(OptionSetting optionSetting)
        {
            Ensure.NotNull(optionSetting, "optionSetting");

            _connectionString = optionSetting.GetOptionValue<string>("ConnectionString");
            _tableName = optionSetting.GetOptionValue<string>("TableName");

            Ensure.NotNull(_connectionString, "_connectionString");
            Ensure.NotNull(_tableName, "_tableName");

            _lockKeySqlFormat = "SELECT * FROM [" + _tableName + "] WHERE [Name] = '{0}' FOR UPDATE";
        }

        #endregion

        public void AddLockKey(string lockKey)
        {
            using (var connection = GetConnection())
            {
                var count = connection.QueryList(new { Name = lockKey }, _tableName).Count();
                if (count == 0)
                {
                    connection.Insert(new { Name = lockKey }, _tableName);
                }
            }
        }
        public void ExecuteInLock(string lockKey, Action action)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
                try
                {
                    LockKey(transaction, lockKey);
                    action();
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private void LockKey(IDbTransaction transaction, string key)
        {
            var sql = string.Format(_lockKeySqlFormat, key);
            transaction.Connection.Query(sql, transaction: transaction);
        }

        private IDbConnection GetConnection()
        {
            return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
        }
    }
}
