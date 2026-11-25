// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Tables;

internal class MetaTable(SqlDatabase database) : ITable
{
    public const string KEY_VERSION = "version";

    public static StringColumn ColumnKey { get; } = new("key");
    public static Int32Column ColumnValue { get; } = new("value");

    private static readonly Lazy<MetaTable> _main = new(() => new MetaTable(SqliteDB.MainDatabase));

    public static MetaTable Main => _main.Value;

    public SqlDatabase Database => database;

    public string TableName => "meta";
}
