// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Data.Sqlite;

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public sealed partial class SqlDatabase : IDisposable
{
    private readonly string _filePath;
    private readonly SqliteConnection _connection;

    public string UniqueId => _filePath;

    public SqlDatabase(string filePath)
    {
        _filePath = filePath;
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
        }

        var connection = new SqliteConnection($"Filename={filePath}");
        connection.Open();
        _connection = connection;
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    public void WithTransaction(Action action)
    {
        using SqliteTransaction transaction = _connection.BeginTransaction();
        try
        {
            action();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal SqliteCommand NewCommand()
    {
        return _connection.CreateCommand();
    }

    public bool TableExists(string tableName)
    {
        CommandWrapper command = new();
        string parameterName = command.AppendParameter(tableName);
        command.SetCommandText($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = {parameterName}");
        object? result = command.ExecuteScalar(this);
        return result is long count && count > 0;
    }
}
