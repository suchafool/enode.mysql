﻿using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Dapper;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Utilities;
using ENode.Configurations;
using System.Data;
using System.Data.Common;

namespace ENode.Infrastructure.Impl.Mysql
{
    public class MySqlSequenceMessagePublishedVersionStore : ISequenceMessagePublishedVersionStore
    {
        #region Private Variables

        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _primaryKeyName;
        private readonly ILogger _logger;

        #endregion

        #region Constructors

        public MySqlSequenceMessagePublishedVersionStore(OptionSetting optionSetting)
        {
            Ensure.NotNull(optionSetting, "optionSetting");

            _connectionString = optionSetting.GetOptionValue<string>("ConnectionString");
            _tableName = optionSetting.GetOptionValue<string>("TableName");
            _primaryKeyName = "PRIMARY"; 

            Ensure.NotNull(_connectionString, "_connectionString");
            Ensure.NotNull(_tableName, "_tableName");

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        #endregion

        public async Task<AsyncTaskResult> UpdatePublishedVersionAsync(string processorName, string aggregateRootTypeName, string aggregateRootId, int publishedVersion)
        {
            if (publishedVersion == 1)
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        await connection.InsertAsync(new
                        {
                            ProcessorName = processorName,
                            AggregateRootTypeName = aggregateRootTypeName,
                            AggregateRootId = aggregateRootId,
                            PublishedVersion = 1,
                            CreatedOn = DateTime.Now
                        }, _tableName);
                        return AsyncTaskResult.Success;
                    }
                }
                catch (MySql.Data.MySqlClient.MySqlException ex)
                {
                    if (ex.Number == 1062 && ex.Message.Contains(_primaryKeyName))
                    {
                        return AsyncTaskResult.Success;
                    }
                    _logger.Error("Insert sequence message published version has sql exception.", ex);
                    return new AsyncTaskResult(AsyncTaskStatus.IOException, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error("Insert sequence message published version has unknown exception.", ex);
                    return new AsyncTaskResult(AsyncTaskStatus.Failed, ex.Message);
                }
            }
            else
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        await connection.UpdateAsync(
                        new
                        {
                            PublishedVersion = publishedVersion,
                            CreatedOn = DateTime.Now
                        },
                        new
                        {
                            ProcessorName = processorName,
                            AggregateRootId = aggregateRootId,
                            PublishedVersion = publishedVersion - 1
                        }, _tableName);
                        return AsyncTaskResult.Success;
                    }
                }
                catch (DbException ex)
                {
                    _logger.Error("Update sequence message published version has sql exception.", ex);
                    return new AsyncTaskResult(AsyncTaskStatus.IOException, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error("Update sequence message published version has unknown exception.", ex);
                    return new AsyncTaskResult(AsyncTaskStatus.Failed, ex.Message);
                }
            }
        }
        public async Task<AsyncTaskResult<int>> GetPublishedVersionAsync(string processorName, string aggregateRootTypeName, string aggregateRootId)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var result = await connection.QueryListAsync<int>(new
                    {
                        ProcessorName = processorName,
                        AggregateRootId = aggregateRootId
                    }, _tableName, "PublishedVersion");
                    return new AsyncTaskResult<int>(AsyncTaskStatus.Success, result.SingleOrDefault());
                }
            }
            catch (DbException ex)
            {
                _logger.Error("Get sequence message published version has sql exception.", ex);
                return new AsyncTaskResult<int>(AsyncTaskStatus.IOException, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Get sequence message published version has unknown exception.", ex);
                return new AsyncTaskResult<int>(AsyncTaskStatus.Failed, ex.Message);
            }
        }

        private IDbConnection GetConnection()
        {
            return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
        }
    }
}
