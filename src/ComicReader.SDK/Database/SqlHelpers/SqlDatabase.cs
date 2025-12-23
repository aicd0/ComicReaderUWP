// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Data.Sqlite;

namespace ComicReader.SDK.Database.SqlHelpers;

public sealed class SqlDatabase : IDisposable
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
}
