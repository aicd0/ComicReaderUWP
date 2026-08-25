// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Tables;

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
    public static StringColumn ColumnDescription { get; } = new("description");
    public static Int32Column ColumnCompletionStatus { get; } = new("completion_state");
    public static Int32Column ColumnPageCount { get; } = new("page_count");
    public static StringColumn ColumnExt { get; } = new("ext");

    private ComicTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqliteDB.MainDatabase;
    }

    public string GetTableName()
    {
        return "comics";
    }
}
