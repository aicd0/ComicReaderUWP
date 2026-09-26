// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Tables;

internal class ComicCollectionTable : ITable
{
    public static ComicCollectionTable Instance { get; } = new();

    public static Int64Column ColumnCollectionId { get; } = new("collection_id");
    public static Int64Column ColumnComicId { get; } = new("comic_id");

    private ComicCollectionTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqliteDB.MainDatabase;
    }

    public string GetTableName()
    {
        return "comic_collections";
    }
}
