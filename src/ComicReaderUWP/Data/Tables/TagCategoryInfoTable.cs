// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Tables;

internal class TagCategoryInfoTable : ITable
{
    public static TagCategoryInfoTable Instance { get; } = new TagCategoryInfoTable();

    public static StringColumn ColumnName { get; } = new("Name");
    public static StringColumn ColumnExt { get; } = new("Ext");

    private TagCategoryInfoTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqliteDB.TagInfoDatabase;
    }

    public string GetTableName()
    {
        return "TagCategoryInfo";
    }
}
