// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Data.Misc;
using ComicReaderUWP.SDK.Database.SqlHelpers;

namespace ComicReaderUWP.Data.Tables;

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
