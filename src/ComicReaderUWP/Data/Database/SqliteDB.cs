// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Data.Database;

public static class SqliteDB
{
    #region Constructor

    private const string TAG = nameof(SqliteDB);

    private static string DatabaseFolderPath => Path.Combine(StorageLocation.LocalFolderPath, "sqlite");

    private static bool _initialized = false;

    #endregion

    #region Public Methods

    public static void Dispose()
    {
        _mainDatabase?.Dispose();
        _mainDatabase = null;
        _tagInfoDatabase?.Dispose();
        _tagInfoDatabase = null;
        _miscDatabase?.Dispose();
        _miscDatabase = null;
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            Logger.F(TAG, "Database is already initialized");
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

    public static void UpgradeDatabase()
    {
        UpgradeMainDatabase();
    }

    #endregion

    #region Comic Database

    public const int MAIN_DATABASE_VERSION = 9;

    private static SqlDatabase? _mainDatabase = null;
    public static SqlDatabase MainDatabase => _mainDatabase!;

    private static readonly ITaskDispatcher _mainDbDispatcher = TaskDispatcher.Factory.NewQueue("MainDatabaseQueue");
    public static ITaskDispatcher MainDatabaseDispatcher => _mainDbDispatcher;

    private static void UpgradeMainDatabase()
    {
        int version = ReadMainDatabaseVersion();
        if (version >= MAIN_DATABASE_VERSION)
        {
            return;
        }

        string comicTable = ComicTable.Instance.TableName;
        MainDatabase.WithTransaction(() =>
        {
            switch (version)
            {
                case -1:
                case 0:
                case 1:
                    goto case MAIN_DATABASE_VERSION;
                case 2:
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} DROP COLUMN image_aspect_ratios");
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} DROP COLUMN cover_file_name");
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN cover_cache_key TEXT DEFAULT ''");
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnDescription.Name} TEXT DEFAULT ''");
                    goto case 3;
                case 3:
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnCompletionStatus.Name} INTEGER NOT NULL DEFAULT 0");
                    goto case 4;
                case 4:
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnExt.Name} TEXT DEFAULT ''");
                    goto case 5;
                case 5:
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnPageCount.Name} INTEGER NOT NULL DEFAULT -1");
                    goto case 6;
                case 6:
                    ExecuteCommand(MainDatabase, $"UPDATE {comicTable} SET {ComicTable.ColumnRating.Name} = {ComicTable.ColumnRating.Name} * 20 WHERE {ComicTable.ColumnRating.Name} >= 0");
                    goto case 7;
                case 7: // 3.3.0
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} DROP COLUMN cover_cache_key");
                    goto case 8;
                case 8: // 3.7.0
                    ExecuteCommand(MainDatabase, $"ALTER TABLE {comicTable} ADD COLUMN {ComicTable.ColumnReleaseDate.Name} TEXT DEFAULT ''");
                    goto case MAIN_DATABASE_VERSION;
                case MAIN_DATABASE_VERSION:
                    break;
                default:
                    Logger.F(TAG, $"Unknown database version: {version}");
                    break;
            }

            WriteMainDatabaseVersion(MAIN_DATABASE_VERSION);
        });
    }

    private static void InitializeMainDatabase()
    {
        _mainDatabase?.Dispose();
        _mainDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "main.db"));

        bool isNewDatabase = !MainDatabase.TableExists(ComicTable.Instance.TableName);
        MainDatabase.WithTransaction(() =>
        {
            CreateMainTables();
            if (isNewDatabase)
            {
                // A new database already has the latest schema, so it only needs its version recorded.
                WriteMainDatabaseVersion(MAIN_DATABASE_VERSION);
            }
            else if (ReadMainDatabaseVersion() == 0)
            {
                // Databases created by earlier releases recorded their schema version in Versions.json.
                WriteMainDatabaseVersion(DatabaseVersionModel.SqliteDatabaseVersion);
            }
        });
    }

    private static void CreateMainTables()
    {
        string metaTable = MetaTable.Main.TableName;
        string comicTable = ComicTable.Instance.TableName;
        string comicCollectionTable = ComicCollectionTable.Instance.TableName;
        string tagCategoryTable = TagCategoryTable.Instance.TableName;
        string tagTable = TagTable.Instance.TableName;

        ExecuteCommand(MainDatabase, "CREATE TABLE IF NOT EXISTS " + metaTable + " (" +
            MetaTable.ColumnKey.Name + " TEXT NOT NULL PRIMARY KEY" +
            "," + MetaTable.ColumnValue.Name + " INTEGER NOT NULL" +
            ")");

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
            "," + ComicTable.ColumnDescription.Name + " TEXT" +
            "," + ComicTable.ColumnCompletionStatus.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnPageCount.Name + " INTEGER NOT NULL" +
            "," + ComicTable.ColumnReleaseDate.Name + " TEXT" +
            "," + ComicTable.ColumnExt.Name + " TEXT" +
            ")");

        ExecuteCommand(MainDatabase, "CREATE TABLE IF NOT EXISTS " + comicCollectionTable + " (" +
            ComicCollectionTable.ColumnCollectionId.Name + " INTEGER NOT NULL REFERENCES " + comicTable + "(" + ComicTable.ColumnId.Name + ") ON DELETE CASCADE" +
            "," + ComicCollectionTable.ColumnComicId.Name + " INTEGER NOT NULL REFERENCES " + comicTable + "(" + ComicTable.ColumnId.Name + ") ON DELETE CASCADE" +
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

    private static int ReadMainDatabaseVersion()
    {
        SelectCommand command = SelectCommand.Create(MetaTable.Main)
            .AppendCondition(MetaTable.ColumnKey, MetaTable.KEY_VERSION);
        IReaderToken<int> valueToken = command.PutQueryInt32(MetaTable.ColumnValue);
        using SelectCommand.IReader reader = command.Execute();
        return reader.Read() ? valueToken.GetValue() : 0;
    }

    private static void WriteMainDatabaseVersion(int version)
    {
        InsertCommand.Create(MetaTable.Main)
            .AppendColumn(MetaTable.ColumnKey, MetaTable.KEY_VERSION)
            .AppendColumn(MetaTable.ColumnValue, version)
            .OnConflict((List<IColumnTypeless>)[MetaTable.ColumnKey])
            .Update(MetaTable.ColumnValue, version)
            .End()
            .Execute();
    }

    #endregion

    #region Tag Info Database

    private static SqlDatabase? _tagInfoDatabase = null;
    public static SqlDatabase TagInfoDatabase => _tagInfoDatabase!;

    private static readonly ITaskDispatcher _tabInfoDbDispatcher = TaskDispatcher.Factory.NewQueue("TagInfoDatabaseQueue");
    public static ITaskDispatcher TagInfoDatabaseDispatcher => _tabInfoDbDispatcher;

    private static void InitializeTagInfoDatabase()
    {
        _tagInfoDatabase?.Dispose();
        _tagInfoDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "tag_info.db"));

        string tagCategoryInfoTable = TagCategoryInfoTable.Instance.TableName;
        string tagInfoTable = TagInfoTable.Instance.TableName;

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

    #endregion

    #region Miscellaneous Database

    private static SqlDatabase? _miscDatabase = null;
    public static SqlDatabase MiscDatabase => _miscDatabase!;

    private static readonly ITaskDispatcher _miscDbDispatcher = TaskDispatcher.Factory.NewQueue("MiscDatabaseQueue");
    public static ITaskDispatcher MiscDatabaseDispatcher => _miscDbDispatcher;

    private static void InitializeMiscDatabase()
    {
        _miscDatabase?.Dispose();
        _miscDatabase = new SqlDatabase(Path.Combine(DatabaseFolderPath, "misc.db"));

        string comicHistoryTable = ComicHistoryTable.Instance.TableName;

        ExecuteCommand(MiscDatabase, "CREATE TABLE IF NOT EXISTS " + comicHistoryTable + " (" +
            ComicHistoryTable.ColumnComicId.Name + " INTEGER PRIMARY KEY" +
            "," + ComicHistoryTable.ColumnTitle.Name + " TEXT NOT NULL" +
            "," + ComicHistoryTable.ColumnTime.Name + " INTEGER NOT NULL" +
            ")");
    }

    #endregion

    #region Helpers

    private static void ExecuteCommand(SqlDatabase database, string commandText)
    {
        UnsafeCommand.Create(commandText)
            .Execute(database);
    }

    #endregion
}
