﻿using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Dapper;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Utilities;
using ENode.Configurations;
using System.Data;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace ENode.Infrastructure.Impl.Mysql
{
    public class MySqlMessageHandleRecordStore : IMessageHandleRecordStore
    {
        #region Private Variables
        
        private readonly string _connectionString;
        private readonly string _oneMessageTableName;
        private readonly string _oneMessageTablePrimaryKeyName;
        private readonly string _twoMessageTableName;
        private readonly string _twoMessageTablePrimaryKeyName;
        private readonly string _threeMessageTableName;
        private readonly string _threeMessageTablePrimaryKeyName;
        private readonly ILogger _logger;

        #endregion

        #region Constructors

        public MySqlMessageHandleRecordStore(OptionSetting optionSetting)
        {
            Ensure.NotNull(optionSetting, "optionSetting");

            _connectionString = optionSetting.GetOptionValue<string>("ConnectionString");
            _oneMessageTableName = optionSetting.GetOptionValue<string>("OneMessageTableName");
            _oneMessageTablePrimaryKeyName = "PRIMARY"; //MySql's primary key name always be "PRIMARY"
            _twoMessageTableName = optionSetting.GetOptionValue<string>("TwoMessageTableName");
            _twoMessageTablePrimaryKeyName = "PRIMARY";  
            _threeMessageTableName = optionSetting.GetOptionValue<string>("ThreeMessageTableName");
            _threeMessageTablePrimaryKeyName = "PRIMARY"; 

            Ensure.NotNull(_connectionString, "_connectionString");
            Ensure.NotNull(_oneMessageTableName, "_oneMessageTableName");
            Ensure.NotNull(_twoMessageTableName, "_twoMessageTableName");
            Ensure.NotNull(_threeMessageTableName, "_threeMessageTableName");
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        #endregion

        public async Task<AsyncTaskResult> AddRecordAsync(MessageHandleRecord record)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.InsertAsync(record, _oneMessageTableName);
                    return AsyncTaskResult.Success;
                }
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (ex.Number == 1062 && ex.Message.Contains(_oneMessageTablePrimaryKeyName))
                {
                    return AsyncTaskResult.Success;
                }
                _logger.Error("Insert message handle record has sql exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Insert message handle record has unknown exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.Failed, ex.Message);
            }
        }
        public async Task<AsyncTaskResult> AddRecordAsync(TwoMessageHandleRecord record)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.InsertToMySqlAsync(record, _twoMessageTableName);
                    return AsyncTaskResult.Success;
                }
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (ex.Number == 1062 && ex.Message.Contains(_twoMessageTablePrimaryKeyName))
                {
                    return AsyncTaskResult.Success;
                }
                _logger.Error("Insert two-message handle record has sql exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Insert two-message handle record has unknown exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.Failed, ex.Message);
            }
        }
        public async Task<AsyncTaskResult> AddRecordAsync(ThreeMessageHandleRecord record)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.InsertToMySqlAsync(record, _threeMessageTableName);
                    return AsyncTaskResult.Success;
                }
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (ex.Number == 1062 && ex.Message.Contains(_threeMessageTablePrimaryKeyName))
                {
                    return AsyncTaskResult.Success;
                }
                _logger.Error("Insert three-message handle record has sql exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Insert three-message handle record has unknown exception.", ex);
                return new AsyncTaskResult(AsyncTaskStatus.Failed, ex.Message);
            }
        }
        public async Task<AsyncTaskResult<bool>> IsRecordExistAsync(string messageId, string handlerTypeName, string aggregateRootTypeName)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var count = await connection.GetCountAsync(new { MessageId = messageId, HandlerTypeName = handlerTypeName }, _oneMessageTableName);
                    return new AsyncTaskResult<bool>(AsyncTaskStatus.Success, count > 0);
                }
            }
            catch (DbException ex)
            {
                _logger.Error("Get message handle record has sql exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Get message handle record has unknown exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.Failed, ex.Message);
            }
        }
        public async Task<AsyncTaskResult<bool>> IsRecordExistAsync(string messageId1, string messageId2, string handlerTypeName, string aggregateRootTypeName)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var count = await connection.GetCountAsync(new { MessageId1 = messageId1, MessageId2 = messageId2, HandlerTypeName = handlerTypeName }, _twoMessageTableName);
                    return new AsyncTaskResult<bool>(AsyncTaskStatus.Success, count > 0);
                }
            }
            catch (DbException ex)
            {
                _logger.Error("Get two-message handle record has sql exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Get two-message handle record has unknown exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.Failed, ex.Message);
            }
        }
        public async Task<AsyncTaskResult<bool>> IsRecordExistAsync(string messageId1, string messageId2, string messageId3, string handlerTypeName, string aggregateRootTypeName)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var count = await connection.GetCountAsync(new { MessageId1 = messageId1, MessageId2 = messageId2, MessageId3 = messageId3, HandlerTypeName = handlerTypeName }, _threeMessageTableName);
                    return new AsyncTaskResult<bool>(AsyncTaskStatus.Success, count > 0);
                }
            }
            catch (DbException ex)
            {
                _logger.Error("Get three-message handle record has sql exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Get three-message handle record has unknown exception.", ex);
                return new AsyncTaskResult<bool>(AsyncTaskStatus.Failed, ex.Message);
            }
        }

        private IDbConnection GetConnection()
        {
            return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
        }
    }
}