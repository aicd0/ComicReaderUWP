// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Tables;

internal class TagTable : ITable
{
    public static TagTable Instance { get; } = new TagTable();

    public static StringColumn ColumnContent { get; } = new("content");
    public static Int64Column ColumnComicId { get; } = new("comic_id");
    public static Int64Column ColumnTagCategoryId { get; } = new("cate_id");

    private TagTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqlDatabaseManager.MainDatabase;
    }

    public string GetTableName()
    {
        return "tags";
    }
}
