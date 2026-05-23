// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Tables;

internal class ComicHistoryTable : ITable
{
    public static ComicHistoryTable Instance { get; } = new ComicHistoryTable();

    public static Int64Column ColumnComicId { get; } = new("comic_id");
    public static StringColumn ColumnTitle { get; } = new("title");
    public static Int64Column ColumnTime { get; } = new("time");

    private ComicHistoryTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqliteDB.MiscDatabase;
    }

    public string GetTableName()
    {
        return "comic_history";
    }
}
