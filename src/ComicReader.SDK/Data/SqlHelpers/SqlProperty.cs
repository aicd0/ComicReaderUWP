// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AutoProperty;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Data.SqlHelpers;

public sealed class SqlProperty<T, K, V>(ITaskDispatcher dispatcher, SqlDatabase database, T table) : AbsProperty<SqlPropertyKey<K, V>, V, SqlPropertyModel<K, V>, IPropertyExtension> where T : ITable where K : notnull
{
    private readonly SqlDatabase _database = database;
    private readonly T _table = table;

    public sealed override SqlPropertyModel<K, V> CreateModel()
    {
        return new SqlPropertyModel<K, V>();
    }

    public sealed override LockResource GetLockResource(SqlPropertyKey<K, V> key, LockType type)
    {
        LockResource actualResource = new()
        {
            Type = type
        };
        LockResource columnResource = new();
        columnResource.Children.Add(key.KeyColumn.Name, actualResource);
        LockResource rowResource = new();
        rowResource.Children.Add(key.ResourceKey, columnResource);
        LockResource tableResource = new();
        tableResource.Children.Add(_table.GetTableName(), rowResource);
        LockResource dbResource = new();
        dbResource.Children.Add(_database.UniqueId, tableResource);
        LockResource resource = new();
        resource.Children.Add("sql_common", dbResource);
        return resource;
    }

    public sealed override void RearrangeRequests(PropertyContext<SqlPropertyKey<K, V>, V, SqlPropertyModel<K, V>, IPropertyExtension> context)
    {
        SqlPropertyModel<K, V> model = context.Model;
        foreach (SealedPropertyRequest<SqlPropertyKey<K, V>, V> request in context.NewRequests)
        {
            model.requests.Add(request);
        }
    }

    public sealed override void ProcessRequests(PropertyContext<SqlPropertyKey<K, V>, V, SqlPropertyModel<K, V>, IPropertyExtension> context, IProcessCallback callback)
    {
        List<SealedPropertyRequest<SqlPropertyKey<K, V>, V>> requests = [.. context.Model.requests];
        context.Model.requests.Clear();
        dispatcher.Submit("", () =>
        {
            Dictionary<IColumn<K>, Dictionary<IColumn<V>, ReadSqlOperation>> readOperations = [];
            bool modifyOperationAdded = false;
            ModifySqlOperation modifyOperation = new();
            var operations = new List<ISqlOperation>();
            Dictionary<long, PropertyResponseContent<V>> responses = [];
            foreach (SealedPropertyRequest<SqlPropertyKey<K, V>, V> request in requests)
            {
                PropertyRequestContent<SqlPropertyKey<K, V>, V> requestContent = request.RequestContent;
                SqlPropertyKey<K, V> key = requestContent.Key;
                switch (requestContent.Type)
                {
                    case RequestType.Read:
                        {
                            if (!readOperations.TryGetValue(key.KeyColumn, out Dictionary<IColumn<V>, ReadSqlOperation>? keyOperations))
                            {
                                keyOperations = [];
                                readOperations[key.KeyColumn] = keyOperations;
                            }
                            if (!keyOperations.TryGetValue(key.ValueColumn, out ReadSqlOperation? readOperation))
                            {
                                readOperation = new(key.KeyColumn, key.ValueColumn);
                                keyOperations[key.ValueColumn] = readOperation;
                                operations.Add(readOperation);
                            }
                            readOperation.Keys[key.SqlKey] = request.Id;
                        }
                        break;
                    case RequestType.Modify:
                        {
                            if (!requestContent.Option.Persistent)
                            {
                                responses[request.Id] = PropertyResponseContent<V>.NewSuccessfulResponse();
                                break;
                            }
                            if (requestContent.Value is null)
                            {
                                Logger.AssertNotReachHere("DC62E07E0415FDBE");
                                responses[request.Id] = PropertyResponseContent<V>.NewFailedResponse();
                                break;
                            }
                            SingleModifySqlOperation operation = new(request.Id, key.KeyColumn, key.ValueColumn, key.SqlKey, requestContent.Value);
                            modifyOperation.Operations.Add(operation);
                            if (!modifyOperationAdded)
                            {
                                modifyOperationAdded = true;
                                operations.Add(modifyOperation);
                            }
                        }
                        break;
                    default:
                        Logger.AssertNotReachHere("E1209CC85F4EA257");
                        responses[request.Id] = PropertyResponseContent<V>.NewFailedResponse();
                        break;
                }
            }

            foreach (ISqlOperation operation in operations)
            {
                operation.Perform(this, responses);
                if (responses.Count > 0)
                {
                    callback.PostOnServerThread(false, () =>
                    {
                        BatchRespond(context, responses);
                    });
                }
            }

            callback.PostOnServerThread(true, () =>
            {
                BatchRespond(context, responses);
            });
        });
    }

    private static void BatchRespond(PropertyContext<SqlPropertyKey<K, V>, V, SqlPropertyModel<K, V>, IPropertyExtension> context, Dictionary<long, PropertyResponseContent<V>> responses)
    {
        foreach (KeyValuePair<long, PropertyResponseContent<V>> response in responses)
        {
            context.Respond(response.Key, response.Value);
        }
        responses.Clear();
    }

    private interface ISqlOperation
    {
        void Perform(SqlProperty<T, K, V> property, Dictionary<long, PropertyResponseContent<V>> responses);
    }

    private class ReadSqlOperation(IColumn<K> keyColumn, IColumn<V> valueColumn) : ISqlOperation
    {
        public readonly IColumn<K> KeyColumn = keyColumn;
        public readonly IColumn<V> ValueColumn = valueColumn;
        public readonly Dictionary<K, long> Keys = [];

        public void Perform(SqlProperty<T, K, V> property, Dictionary<long, PropertyResponseContent<V>> responses)
        {
            var command = SelectCommand.Create(property._table);
            command.AppendCondition(new InCondition(ColumnOrValue.FromColumn(KeyColumn), Keys.Keys));
            IReaderToken<K> keyToken = KeyColumn.PutQuery(command);
            IReaderToken<V> valueToken = ValueColumn.PutQuery(command);
            SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                K key = keyToken.GetValue();
                V value = valueToken.GetValue();
                if (Keys.Remove(key, out long requestId))
                {
                    responses[requestId] = PropertyResponseContent<V>.NewSuccessfulResponse(value);
                }
            }
            foreach (KeyValuePair<K, long> kvp in Keys)
            {
                responses[kvp.Value] = PropertyResponseContent<V>.NewFailedResponse();
            }
        }
    }

    private class ModifySqlOperation : ISqlOperation
    {
        public readonly List<SingleModifySqlOperation> Operations = [];

        public void Perform(SqlProperty<T, K, V> property, Dictionary<long, PropertyResponseContent<V>> responses)
        {
            property._database.WithTransaction(() =>
            {
                foreach (SingleModifySqlOperation operation in Operations)
                {
                    var command = UpdateCommand.Create(property._table);
                    command.AppendColumn(operation.ValueColumn, operation.Value);
                    command.AppendCondition(operation.KeyColumn, operation.Key);
                    command.Execute();
                }
            });
            foreach (SingleModifySqlOperation operation in Operations)
            {
                responses[operation.RequestId] = PropertyResponseContent<V>.NewSuccessfulResponse();
            }
        }
    }

    private class SingleModifySqlOperation(long requestId, IColumn<K> keyColumn, IColumn<V> valueColumn, K key, V value)
    {
        public readonly long RequestId = requestId;
        public readonly IColumn<K> KeyColumn = keyColumn;
        public readonly IColumn<V> ValueColumn = valueColumn;
        public readonly K Key = key;
        public readonly V Value = value;
    }
}
