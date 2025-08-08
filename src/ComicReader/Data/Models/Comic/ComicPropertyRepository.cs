// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Data.Tables;
using ComicReader.SDK.Common.AutoProperty;
using ComicReader.SDK.Common.AutoProperty.Presets;
using ComicReader.SDK.Common.AutoProperty.Utils;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models.Comic;

internal class ComicPropertyRepository
{
    public static readonly ComicPropertyRepository Instance = new();

    private readonly PropertyServer _server = new("ComicPropertyRepository");

    private SqlProperty<ComicTable, long, int> CompletionStateSql { get; }
    private ConverterProperty<ComicIdKey, SqlPropertyKey<long, int>, int, ComicCompletionStatusEnum> CompletionStateConverter { get; }
    private MemoryCacheProperty<ComicIdKey, ComicCompletionStatusEnum> CompletionStateCache { get; }
    public SimplePropertyOperator<long, ComicIdKey, ComicCompletionStatusEnum> CompletionStateOperator { get; }

    private ComicPropertyRepository()
    {
        CompletionStateSql = new(SqlDatabaseManager.MainDatabaseDispatcher, SqlDatabaseManager.MainDatabase, ComicTable.Instance);
        CompletionStateConverter = new(CompletionStateSql, (k) => new(ComicTable.ColumnId, ComicTable.ColumnCompletionState, k.Id, k.Id.ToString()),
            (v) => (int)v, (r) => r.WithValue(ParseCompletionState(r.Value)));
        CompletionStateCache = new(CompletionStateConverter);
        CompletionStateOperator = new(_server, CompletionStateCache, (k) => new(k));
    }

    public static ComicCompletionStatusEnum ParseCompletionState(int value)
    {
        if (Enum.IsDefined(typeof(ComicCompletionStatusEnum), value))
        {
            return (ComicCompletionStatusEnum)value;
        }
        return ComicCompletionStatusEnum.NotStarted;
    }
}
