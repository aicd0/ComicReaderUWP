// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.SDK.Common.DebugTools;

using Microsoft.Data.Sqlite;

namespace ComicReader.SDK.Database.SqlHelpers;

public sealed class CommandWrapper : ICommandContext
{
    private int _parameterIndex = 0;
    private readonly Dictionary<string, object> _parameters = [];
    private string _commandText = string.Empty;

    public string AppendParameter(object value)
    {
        string parameterName = $"@param{_parameterIndex++}";
        _parameters.Add(parameterName, value);
        return parameterName;
    }

    public void SetCommandText(string commandText)
    {
        _commandText = commandText;
    }

    public int ExecuteNonQuery(SqlDatabase database)
    {
        LogCommand();
        SqliteCommand command = CreateCommand(database);
        return command.ExecuteNonQuery();
    }

    public async Task<int> ExecuteNonQueryAsync(SqlDatabase database)
    {
        LogCommand();
        SqliteCommand command = CreateCommand(database);
        return await command.ExecuteNonQueryAsync();
    }

    public object? ExecuteScalar(SqlDatabase database)
    {
        LogCommand();
        SqliteCommand command = CreateCommand(database);
        return command.ExecuteScalar();
    }

    public SqliteDataReader ExecuteReader(SqlDatabase database)
    {
        LogCommand();
        SqliteCommand command = CreateCommand(database);
        return command.ExecuteReader();
    }

    public async Task<SqliteDataReader> ExecuteReaderAsync(SqlDatabase database)
    {
        LogCommand();
        SqliteCommand command = CreateCommand(database);
        return await command.ExecuteReaderAsync();
    }

    public override string ToString()
    {
        StringBuilder sb = new(_commandText);
        foreach (KeyValuePair<string, object> parameter in _parameters)
        {
            sb.Replace(parameter.Key, ValueToStringRepresentation(parameter.Value));
        }

        return sb.ToString();
    }

    private SqliteCommand CreateCommand(SqlDatabase database)
    {
        SqliteCommand command = database.NewCommand();
        command.CommandType = System.Data.CommandType.Text;
#pragma warning disable CA2100
        command.CommandText = _commandText;
#pragma warning restore CA2100
        foreach (KeyValuePair<string, object> parameter in _parameters)
        {
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        }

        return command;
    }

    private void LogCommand()
    {
        Logger.I("SQLCommand", ToString());
    }

    private static string ValueToStringRepresentation(object value)
    {
        if (value is bool booleanValue)
        {
            return booleanValue ? "TRUE" : "FALSE";
        }

        if (value is string stringValue)
        {
            return "\"" + stringValue.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        return value.ToString() ?? "NULL";
    }
}
