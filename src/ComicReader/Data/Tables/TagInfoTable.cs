// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Tables;

internal class TagInfoTable : ITable
{
    public static TagInfoTable Instance { get; } = new TagInfoTable();

    public static StringColumn ColumnTagCategory { get; } = new("TagCategory");
    public static StringColumn ColumnTag { get; } = new("Tag");
    public static StringColumn ColumnExt { get; } = new("Ext");

    private TagInfoTable() { }

    public SqlDatabase GetDatabase()
    {
        return SqlDatabaseManager.TagInfoDatabase;
    }

    public string GetTableName()
    {
        return "TagInfo";
    }
}
