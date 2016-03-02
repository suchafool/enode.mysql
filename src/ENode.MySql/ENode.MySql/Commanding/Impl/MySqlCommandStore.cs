﻿using ECommon.Components;
using ECommon.Dapper;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Serializing;
using ECommon.Utilities;
using ENode.Configurations;
using ENode.Infrastructure;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace ENode.Commanding.Impl
{
    public class MySqlCommandStore:ICommandStore
    {
        #region Private Variables

        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _primaryKeyName;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ITypeNameProvider _typeNameProvider;
        private readonly IOHelper _ioHelper;
        private readonly ILogger _logger;

        #region Constructors

        /// <summary>Default constructor.
        /// </summary>
        public MySqlCommandStore(OptionSetting optionSetting)
        {
            Ensure.NotNull(optionSetting, "optionSetting");

            _connectionString = optionSetting.GetOptionValue<string>("ConnectionString");
            _tableName = optionSetting.GetOptionValue<string>("TableName");
            _primaryKeyName = "PRIMARY";

            Ensure.NotNull(_connectionString, "_connectionString");
            Ensure.NotNull(_tableName, "_tableName");

            _jsonSerializer = ObjectContainer.Resolve<IJsonSerializer>();
            _typeNameProvider = ObjectContainer.Resolve<ITypeNameProvider>();
            _ioHelper = ObjectContainer.Resolve<IOHelper>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        #endregion

        #endregion

        #region Public Methods

        public Task<AsyncTaskResult<CommandAddResult>> AddAsync(HandledCommand handledCommand)
        {
            var record = ConvertTo(handledCommand);

            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<CommandAddResult>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        await connection.InsertToMySqlAsync(record, _tableName);
                        return new AsyncTaskResult<CommandAddResult>(AsyncTaskStatus.Success, null, CommandAddResult.Success);
                    }
                }
                catch (MySql.Data.MySqlClient.MySqlException ex)
                {
                    if (ex.Number == 1062 && ex.Message.Contains(_primaryKeyName))
                    {
                        return new AsyncTaskResult<CommandAddResult>(AsyncTaskStatus.Success, null, CommandAddResult.DuplicateCommand);
                    }
                    _logger.Error(string.Format("Add handled command has sql exception, handledCommand: {0}", handledCommand), ex);
                    return new AsyncTaskResult<CommandAddResult>(AsyncTaskStatus.IOException, ex.Message, CommandAddResult.Failed);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Add handled command has unkown exception, handledCommand: {0}", handledCommand), ex);
                    return new AsyncTaskResult<CommandAddResult>(AsyncTaskStatus.Failed, ex.Message, CommandAddResult.Failed);
                }
            }, "AddCommandAsync");
        }
        public Task<AsyncTaskResult<HandledCommand>> GetAsync(string commandId)
        {
            return _ioHelper.TryIOFuncAsync<AsyncTaskResult<HandledCommand>>(async () =>
            {
                try
                {
                    using (var connection = GetConnection())
                    {
                        var result = await connection.QueryListAsync<CommandRecord>(new { CommandId = commandId }, _tableName);
                        var record = result.SingleOrDefault();
                        var handledCommand = record != null ? ConvertFrom(record) : null;
                        return new AsyncTaskResult<HandledCommand>(AsyncTaskStatus.Success, handledCommand);
                    }
                }
                catch (SqlException ex)
                {
                    _logger.Error(string.Format("Get handled command has sql exception, commandId: {0}", commandId), ex);
                    return new AsyncTaskResult<HandledCommand>(AsyncTaskStatus.IOException, ex.Message, null);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Get handled command has unkown exception, commandId: {0}", commandId), ex);
                    return new AsyncTaskResult<HandledCommand>(AsyncTaskStatus.Failed, ex.Message, null);
                }
            }, "GetCommandAsync");
        }

        #endregion

        #region Private Methods

        private IDbConnection GetConnection()
        {
            return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
        }
        private CommandRecord ConvertTo(HandledCommand handledCommand)
        {
            return new CommandRecord
            {
                CommandId = handledCommand.CommandId,
                AggregateRootId = handledCommand.AggregateRootId,
                MessagePayload = handledCommand.Message != null ? _jsonSerializer.Serialize(handledCommand.Message) : null,
                MessageTypeName = handledCommand.Message != null ? _typeNameProvider.GetTypeName(handledCommand.Message.GetType()) : null,
                CreatedOn = DateTime.Now,
            };
        }
        private HandledCommand ConvertFrom(CommandRecord record)
        {
            var message = default(IApplicationMessage);

            if (!string.IsNullOrEmpty(record.MessageTypeName))
            {
                var messageType = _typeNameProvider.GetType(record.MessageTypeName);
                message = _jsonSerializer.Deserialize(record.MessagePayload, messageType) as IApplicationMessage;
            }

            return new HandledCommand(record.CommandId, record.AggregateRootId, message);
        }

        #endregion

        class CommandRecord
        {
            public string CommandId { get; set; }
            public string AggregateRootId { get; set; }
            public string MessagePayload { get; set; }
            public string MessageTypeName { get; set; }
            public DateTime CreatedOn { get; set; }
        }
    }
}
