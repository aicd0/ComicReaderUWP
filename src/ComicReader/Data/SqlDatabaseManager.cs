// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Data;

public static class SqlDatabaseManager
{
    public const int DATABASE_VERSION = 7;

    private const string TAG = nameof(SqlDatabaseManager);

    private static string DatabaseFolderPath => Path.Combine(StorageLocation.LocalFolderPath, "database_sql");

    private static bool _initialized = false;

    //
    // Comic Database
    //

    private static SqlDatabase? _mainDatabase = null;
    public static SqlDatabase MainDatabase => _mainDatabase!;

    private static readonly ITaskDispatcher _mainDbDispatcher = TaskDispatcher.Factory.NewQueue("MainDatabaseQueue");
    public static ITaskDispatcher MainDatabaseDispatcher => _mainDbDispatcher;

    //
    // Tag Info Database
    //

    private static SqlDatabase? _tagInfoDatabase = null;
    public static SqlDatabase TagInfoDatabase => _tagInfoDatabase!;

    private static readonly ITaskDispatcher _tabInfoDbDispatcher = TaskDispatcher.Factory.NewQueue("TagInfoDatabaseQueue");
    public static ITaskDispatcher TagInfoDatabaseDispatcher => _tabInfoDbDispatcher;

    //
    // Miscellaneous Database
    //

    private static SqlDatabase? _miscDatabase = null;
    public static SqlDatabase MiscDatabase => _miscDatabase!;

    private static readonly ITaskDispatcher _miscDbDispatcher = TaskDispatcher.Factory.NewQueue("MiscDatabaseQueue");
    public static ITaskDispatcher MiscDatabaseDispatcher => _miscDbDispatcher;

    //
    // Public Methods
    //

    public static void Initialize()
    {
        if (_initialized)
        {
            Logger.F(TAG, "Database is already initialized.");
            return;
        }

        string databasePath = DatabaseFolderPath;
        if (!Directory.Exists(databasePath))
        {
            Directory.CreateDirectory(databasePath);
        }

        InitializeMainDatabase();
        InitializeTagInfoDatabase();
        InitializeMiscDatabase();
        _initialized = true;
    }

    public static void UpdateDatabase(int databaseVersion)
    {
        string comicTable = ComicTable.Instance.GetTableName();
        switch (databaseVersion)
        {
            case -1:
            case 0:
            case 1:
                goto case DATABASE_VERSION;
            case 2:
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} DROP COLUMN image_aspect_ratios");
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} DROP COLUMN cover_file_name");
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnCoverCacheKey.Name} TEXT DEFAULT ''");
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnDescription.Name} TEXT DEFAULT ''");
                goto case 3;
            case 3:
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnCompletionState.Name} INTEGER NOT NULL DEFAULT 0");
                goto case 4;
            case 4:
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnExt.Name} TEXT DEFAULT ''");
                goto case 5;
            case 5:
                ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnPageCount.Name} INTEGER NOT NULL DEFAULT -1");
                goto case 6;
            case 6:
                ExecuteCommand(MainDatabase, $"UPDATE {comicTable} SET {ComicTable.ColumnRating.Name} = {ComicTable.ColumnRating.Name} * 20 WHERE {ComicTable.ColumnRating.Name} >= 0");
                goto case DATABASE_VERSION;
            case DATABASE_VERSION:
                break;
            default:
                Logger.AssertNotReachHere("A39EA189ED8BB40B");
                break;
        }
    }

    private static void InitializeMainDatabase()
    {
        _mainDatabase?.Dispose();
        _mainDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "main.db"));

        string comicTable = ComicTable.Instance.GetTableName();
        string tagCategoryTable = TagCategoryTable.Instance.GetTableName();
        string tagTable = TagTable.Instance.GetTableName();

        ExecuteCommand(MainDatabase, "CREATE TABLE IF NOT EXISTS " + comicTable + " (" +
            ComicTable.ColumnId.Name + " INTEGER PRIMARY KEY AUTOINCREMENT" +
            "," + ComicTable.ColumnType.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnLocation.Name + " TEXT NOT NULL" +
            "," + ComicTable.ColumnTitle1.Name + " TEXT" +
            "," + ComicTable.ColumnTitle2.Name + " TEXT" +
            "," + ComicTable.ColumnHidden.Name + " BOOLEAN NOT NULL" +
            "," + ComicTable.ColumnRating.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnProgress.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnLastVisit.Name + " TIMESTAMP NOT NULL" +
            "," + ComicTable.ColumnLastPosition.Name + " REAL NOT NULL" +
            "," + ComicTable.ColumnCoverCacheKey.Name + " TEXT" +
            "," + ComicTable.ColumnDescription.Name + " TEXT" +
            "," + ComicTable.ColumnCompletionState.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnPageCount.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnExt.Name + " TEXT" +
            ")");

        ExecuteCommand(MainDatabase, "CREATE TABLE IF NOT EXISTS " + tagCategoryTable + " (" +
            TagCategoryTable.ColumnId.Name + " INTEGER PRIMARY KEY AUTOINCREMENT" +
            "," + TagCategoryTable.ColumnName.Name + " TEXT" +
            "," + TagCategoryTable.ColumnComicId.Name + " INTEGER REFERENCES " + comicTable + "(" + ComicTable.ColumnId.Name + ") ON DELETE CASCADE" +
            ")");

        ExecuteCommand(MainDatabase, "CREATE TABLE IF NOT EXISTS " + tagTable + " (" +
            TagTable.ColumnContent.Name + " TEXT" +
            "," + TagTable.ColumnComicId.Name + " INTEGER NOT NULL" +
            "," + TagTable.ColumnTagCategoryId.Name + " INTEGER REFERENCES " + tagCategoryTable + "(" + TagCategoryTable.ColumnId.Name + ") ON DELETE CASCADE" +
            ")");
    }

    private static void InitializeTagInfoDatabase()
    {
        _tagInfoDatabase?.Dispose();
        _tagInfoDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "tag_info.db"));

        string tagCategoryInfoTable = TagCategoryInfoTable.Instance.GetTableName();
        string tagInfoTable = TagInfoTable.Instance.GetTableName();

        ExecuteCommand(TagInfoDatabase, "CREATE TABLE IF NOT EXISTS " + tagCategoryInfoTable + " (" +
            TagCategoryInfoTable.ColumnName.Name + " TEXT NOT NULL PRIMARY KEY" +
            "," + TagCategoryInfoTable.ColumnExt.Name + " TEXT" +
            ")");

        ExecuteCommand(TagInfoDatabase, "CREATE TABLE IF NOT EXISTS " + tagInfoTable + " (" +
            TagInfoTable.ColumnName.Name + " TEXT NOT NULL" +
            "," + TagInfoTable.ColumnTagCategory.Name + " TEXT NOT NULL REFERENCES " + tagCategoryInfoTable + "(" + TagCategoryInfoTable.ColumnName.Name + ") ON DELETE CASCADE ON UPDATE CASCADE" +
            "," + TagInfoTable.ColumnExt.Name + " TEXT" +
            ")");
    }

    private static void InitializeMiscDatabase()
    {
        _miscDatabase?.Dispose();
        _miscDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "misc.db"));

        string comicHistoryTable = ComicHistoryTable.Instance.GetTableName();

        ExecuteCommand(MiscDatabase, "CREATE TABLE IF NOT EXISTS " + comicHistoryTable + " (" +
            ComicHistoryTable.ColumnComicId.Name + " INTEGER PRIMARY KEY" +
            "," + ComicHistoryTable.ColumnTitle.Name + " TEXT NOT NULL" +
            "," + ComicHistoryTable.ColumnTime.Name + " INTEGER NOT NULL" +
            ")");
    }

    private static void ExecuteCommand(SqlDatabase database, string commandText)
    {
        UnsafeCommand.Create(commandText)
            .Execute(database);
    }
}
