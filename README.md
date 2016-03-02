# enode.mysql
Implement MySql Event Store for the popular CQRS Framework enode.


# Usage

    _configuration = Configuration
            .Create()
            .UseAutofac()
            .RegisterCommonComponents()
            .UseLog4Net()
            .UseJsonNet()
            .RegisterUnhandledExceptionHandler()
            .CreateENode()
            .RegisterENodeComponents()
            .RegisterBusinessComponents(assemblies)
            .UseMySqlLockService(new OptionSetting(
                new KeyValuePair<string, object>("ConnectionString", "Server=192.168.1.103;Database=enode;Uid=root;Pwd=123456"),
                new KeyValuePair<string, object>("TableName", "LockKey")))
            .UseMySqlCommandStore(new OptionSetting (
                new KeyValuePair<string,object>("ConnectionString", "Server=192.168.1.103;Database=enode;Uid=root;Pwd=123456"),
                new KeyValuePair<string,object>("TableName","Command"),
                new KeyValuePair<string,object>("PrimaryKeyName", "PRIMARY")))
            .UseMySqlEventStore(new OptionSetting(
                new KeyValuePair<string, object>("ConnectionString", "Server=192.168.1.103;Database=enode;Uid=root;Pwd=123456"),
                new KeyValuePair<string, object>("TableName","EventStream"),
                new KeyValuePair<string, object>("PrimaryKeyName", "PRIMARY"),
                new KeyValuePair<string, object>("CommandIndexName", "IX_EventStream_AggId_CommandId"),
                new KeyValuePair<string, object>("BulkCopyBatchSize", 1000),
                new KeyValuePair<string, object>("BulkCopyTimeout", 60)))
            .UseEQueue()
            .InitializeBusinessAssemblies(assemblies)
            .StartEQueue();