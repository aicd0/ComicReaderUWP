// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Tables;

internal class ComicTable : ITable
{
    public static ComicTable Instance { get; } = new ComicTable();

    public static Int64Column ColumnId { get; } = new("id");
    public static Int64Column ColumnType { get; } = new("type");
    public static StringColumn ColumnLocation { get; } = new("location");
    public static StringColumn ColumnTitle1 { get; } = new("title1");
    public static StringColumn ColumnTitle2 { get; } = new("title2");
    public static BooleanColumn ColumnHidden { get; } = new("hidden");
    public static Int32Column ColumnRating { get; } = new("rating");
    public static Int32Column ColumnProgress { get; } = new("progress");
    public static DateTimeOffsetColumn ColumnLastVisit { get; } = new("last_visit");
    public static DoubleColumn ColumnLastPosition { get; } = new("last_pos");
    public static StringColumn ColumnCoverCacheKey { get; } = new("cover_cache_key");
    public static StringColumn ColumnDescription { get; } = new("description");
    public static Int32Column ColumnCompletionState { get; } = new("completion_state");
    public static StringColumn ColumnExt { get; } = new("ext");

    private ComicTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqlDatabaseManager.MainDatabase;
    }

    public string GetTableName()
    {
        return "comics";
    }
}
