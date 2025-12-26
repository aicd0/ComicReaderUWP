// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Misc;
using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Data.Tables;

internal class TagCategoryInfoTable : ITable
{
    public static TagCategoryInfoTable Instance { get; } = new TagCategoryInfoTable();

    public static StringColumn ColumnName { get; } = new("Name");
    public static StringColumn ColumnExt { get; } = new("Ext");

    private TagCategoryInfoTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqlDatabaseManager.TagInfoDatabase;
    }

    public string GetTableName()
    {
        return "TagCategoryInfo";
    }
}
