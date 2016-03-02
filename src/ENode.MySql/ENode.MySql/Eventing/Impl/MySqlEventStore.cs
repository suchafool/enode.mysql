﻿using Dapper;
using ECommon.Components;
using ECommon.Dapper;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Serializing;
using ECommon.Utilities;
using ENode.Configurations;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ENode.Eventing.Impl
{
    public class MySqlEventStore : IEventStore
    {
        #region Private Variables

        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _primaryKeyName;
        private readonly string _commandIndexName;
        private readonly int _bulkCopyBatchSize;
        private readonly int _bulkCopyTimeout;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IEventSerializer _eventSerializer;
        private readonly IOHelper _ioHelper;
        private readonly ILogger _logger;

        public bool SupportBatchAppend
        {
            get
            {
                return true;
            }
        }

        #endregion

        #region Constructors

        public MySqlEventStore(OptionSetting optionSetting)
        {
            Ensure.NotNull(optionSetting, "optionSetting");

            _connectionString = optionSetting.GetOptionValue<string>("ConnectionString");
            _tableName = optionSetting.GetOptionValue<string>("TableName");
            _primaryKeyName = "PRIMARY"; 
            _commandIndexName = optionSetting.GetOptionValue<string>("CommandIndexName");
            _bulkCopyBatchSize = optionSetting.GetOptionValue<int>("BulkCopyBatchSize");
            _bulkCopyTimeout = optionSetting.GetOptionValue<int>("BulkCopyTimeout");

            Ensure.NotNull(_connectionString, "_connectionString");
            Ensure.NotNull(_tableName, "_tableName");
            Ensure.NotNull(_commandIndexName, "_commandIndexName");
            Ensure.Positive(_bulkCopyBatchSize, "_bulkCopyBatchSize");
            Ensure.Positive(_bulkCopyTimeout, "_bulkCopyTimeout");

            _jsonSerializer = ObjectContainer.Resolve<IJsonSerializer>();
            _eventSerializer = ObjectContainer.Resolve<IEventSerializer>();
            _ioHelper = ObjectContainer.Resolve<IOHelper>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        #endregion

        #region Public Methods

        public IEnumerable<DomainEventStream> QueryAggregateEvents(string aggregateRootId, string aggregateRootTypeName, int minVersion, int maxVersion)
        {
            var records = _ioHelper.TryIOFunc(() =>
            {
                using (var connection = GetConnection())
                {
                    var sql = string.Format("SELECT * FROM [{0}] WHERE AggregateRootId = @AggregateRootId AND Version >= @MinVersion AND Version <= @MaxVersion", _tableName);
                    return connection.Query<StreamRecord>(sql, new
                    {
                        AggregateRootId = aggregateRootId,
                        MinVersion = minVersion,
                        MaxVersion = maxVersion
                    });
                }
            }, "QueryAggregateEvents");

            return records.Select(record => ConvertFrom(record));
        }
        public Task<AsyncTaskResult<EventAppendResult>> AppendAsync(DomainEventStream eventStream)
        {
            var record = ConvertTo(eventStream);

            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<EventAppendResult>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        await connection.InsertToMySqlAsync(record, _tableName);
                        
                        return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Success, EventAppendResult.Success);
                    }
                }
                catch (MySql.Data.MySqlClient.MySqlException ex)
                {
                    if (ex.Number == 1062 && ex.Message.Contains(_primaryKeyName))
                    {
                        return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Success, EventAppendResult.DuplicateEvent);
                    }
                    else if (ex.Number == 1062 && ex.Message.Contains(_commandIndexName))
                    {
                        return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Success, EventAppendResult.DuplicateCommand);
                    }

                    _logger.Error(string.Format("Append event has sql exception, eventStream: {0}", eventStream), ex);
                    return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.IOException, ex.Message, EventAppendResult.Failed);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Append event has unknown exception, eventStream: {0}", eventStream), ex);
                    return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Failed, ex.Message, EventAppendResult.Failed);
                }
            }, "AppendEventsAsync");
        }
        public Task<AsyncTaskResult<DomainEventStream>> FindAsync(string aggregateRootId, int version)
        {
            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<DomainEventStream>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        var result = await connection.QueryListAsync<StreamRecord>(new { AggregateRootId = aggregateRootId, Version = version }, _tableName);
                        var record = result.SingleOrDefault();
                        var stream = record != null ? ConvertFrom(record) : null;
                        return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.Success, stream);
                    }
                }
                catch (DbException ex)
                {
                    _logger.Error(string.Format("Find event by version has sql exception, aggregateRootId: {0}, version: {1}", aggregateRootId, version), ex);
                    return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.IOException, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Find event by version has unknown exception, aggregateRootId: {0}, version: {1}", aggregateRootId, version), ex);
                    return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.Failed, ex.Message);
                }
            }, "FindEventByVersionAsync");
        }
        public Task<AsyncTaskResult<DomainEventStream>> FindAsync(string aggregateRootId, string commandId)
        {
            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<DomainEventStream>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        var result = await connection.QueryListAsync<StreamRecord>(new { AggregateRootId = aggregateRootId, CommandId = commandId }, _tableName);
                        var record = result.SingleOrDefault();
                        var stream = record != null ? ConvertFrom(record) : null;
                        return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.Success, stream);
                    }
                }
                catch (DbException ex)
                {
                    _logger.Error(string.Format("Find event by commandId has sql exception, aggregateRootId: {0}, commandId: {1}", aggregateRootId, commandId), ex);
                    return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.IOException, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Find event by commandId has unknown exception, aggregateRootId: {0}, commandId: {1}", aggregateRootId, commandId), ex);
                    return new AsyncTaskResult<DomainEventStream>(AsyncTaskStatus.Failed, ex.Message);
                }
            }, "FindEventByCommandIdAsync");
        }
        public Task<AsyncTaskResult<IEnumerable<DomainEventStream>>> QueryAggregateEventsAsync(string aggregateRootId, string aggregateRootTypeName, int minVersion, int maxVersion)
        {
            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<IEnumerable<DomainEventStream>>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        var sql = string.Format("SELECT * FROM [{0}] WHERE AggregateRootId = @AggregateRootId AND Version >= @MinVersion AND Version <= @MaxVersion", _tableName);
                        var result = await connection.QueryAsync<StreamRecord>(sql, new
                        {
                            AggregateRootId = aggregateRootId,
                            MinVersion = minVersion,
                            MaxVersion = maxVersion
                        });
                        var streams = result.Select(record => ConvertFrom(record));
                        return new AsyncTaskResult<IEnumerable<DomainEventStream>>(AsyncTaskStatus.Success, streams);
                    }
                }
                catch (DbException ex)
                {
                    _logger.Error(string.Format("Query aggregate events has sql exception, aggregateRootId: {0}", aggregateRootId), ex);
                    return new AsyncTaskResult<IEnumerable<DomainEventStream>>(AsyncTaskStatus.IOException, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Query aggregate events has unknown exception, aggregateRootId: {0}", aggregateRootId), ex);
                    return new AsyncTaskResult<IEnumerable<DomainEventStream>>(AsyncTaskStatus.Failed, ex.Message);
                }
            }, "QueryAggregateEventsAsync");
        }

        #endregion

        #region Private Methods


        private IDbConnection GetConnection()
        {
            return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
        }
        private DomainEventStream ConvertFrom(StreamRecord record)
        {
            return new DomainEventStream(
                record.CommandId,
                record.AggregateRootId,
                record.AggregateRootTypeName,
                record.Version,
                record.CreatedOn,
                _eventSerializer.Deserialize<IDomainEvent>(_jsonSerializer.Deserialize<IDictionary<string, string>>(record.Events)));
        }
        private StreamRecord ConvertTo(DomainEventStream eventStream)
        {
            return new StreamRecord
            {
                CommandId = eventStream.CommandId,
                AggregateRootId = eventStream.AggregateRootId,
                AggregateRootTypeName = eventStream.AggregateRootTypeName,
                Version = eventStream.Version,
                CreatedOn = eventStream.Timestamp,
                Events = _jsonSerializer.Serialize(_eventSerializer.Serialize(eventStream.Events))
            };
        }
        private void AddDataRow(DataTable table, DomainEventStream eventStream)
        {
            var row = table.NewRow();
            row["AggregateRootId"] = eventStream.AggregateRootId;
            row["AggregateRootTypeName"] = eventStream.AggregateRootTypeName;
            row["CommandId"] = eventStream.CommandId;
            row["Version"] = eventStream.Version;
            row["CreatedOn"] = eventStream.Timestamp;
            row["Events"] = _jsonSerializer.Serialize(_eventSerializer.Serialize(eventStream.Events));
            table.Rows.Add(row);
        }

        public IEnumerable<DomainEventStream> QueryByPage(int pageIndex, int pageSize)
        {
            IEnumerable<DomainEventStream> result = null;

            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();

                    var sql = string.Format("SELECT * FROM {0} LIMIT {1},{2}", _tableName, (pageIndex-1) * pageSize, pageSize);

                    var records = connection.Query<StreamRecord>(sql);

                    result = records.Select(record => ConvertFrom(record));
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Exception ocurred when query events.", ex);
            }

            return result;
        }

        public async Task<AsyncTaskResult> BatchAppendAsync(IEnumerable<DomainEventStream> eventStreams)
        {
            if (eventStreams.Count() == 0)
            {
                throw new ArgumentException("Event streams cannot be empty.");
            }
            var aggregateRootIds = eventStreams.Select(x => x.AggregateRootId).Distinct();
            if (aggregateRootIds.Count() > 1)
            {
                throw new ArgumentException("Batch append event only support for one aggregate.");
            }
            var aggregateRootId = aggregateRootIds.Single();


            return await _ioHelper.TryIOFuncAsync(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();

                        var tran = connection.BeginTransaction();
                        try
                        {

                            foreach (var item in eventStreams)
                            {
                                await connection.InsertToMySqlAsync(item, _tableName, tran);
                            }

                            tran.Commit();
                        }
                        catch
                        {
                            try { tran.Rollback(); } catch { }
                            throw;
                        }

                        return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Success, EventAppendResult.Success);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Batch append event has unknown exception.", ex);
                    return new AsyncTaskResult<EventAppendResult>(AsyncTaskStatus.Failed, ex.Message, EventAppendResult.Failed);
                }
            }, "BatchAppendEventsAsync");
        }

        public async Task<AsyncTaskResult<IEnumerable<DomainEventStream>>> QueryByPageAsync(int pageIndex, int pageSize)
        {
            return await _ioHelper.TryIOFuncAsync(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        connection.Open();

                        var sql = string.Format("SELECT * FROM {0} LIMIT {1},{2}", _tableName, (pageIndex - 1) * pageSize, pageSize);

                        var records = await connection.QueryAsync<StreamRecord>(sql);

                        var streams = records.Select(record => ConvertFrom(record));


                        return new AsyncTaskResult<IEnumerable<DomainEventStream>>(AsyncTaskStatus.Success, streams);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Batch append event has unknown exception.", ex);
                    return new AsyncTaskResult<IEnumerable<DomainEventStream>>(AsyncTaskStatus.Failed, ex.Message);
                }
            }, "QueryByPageAsync");
        }

        #endregion

        class StreamRecord
        {
            public string AggregateRootTypeName { get; set; }
            public string AggregateRootId { get; set; }
            public int Version { get; set; }
            public string CommandId { get; set; }
            public DateTime CreatedOn { get; set; }
            public string Events { get; set; }
        }
    }
}
