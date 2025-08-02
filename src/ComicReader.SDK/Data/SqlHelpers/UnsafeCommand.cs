// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.SqlHelpers;

public class UnsafeCommand
{
    private readonly string _command;

    private bool _executed = false;

    private UnsafeCommand(string command)
    {
        _command = command;
    }

    public static UnsafeCommand Create(string command)
    {
        return new(command);
    }

    public void Execute(SqlDatabase database)
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        CommandWrapper command = GenerateCommand();
        command.ExecuteNonQuery(database);
    }

    public async Task ExecuteAsync(SqlDatabase database)
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        CommandWrapper command = GenerateCommand();
        await command.ExecuteNonQueryAsync(database);
    }

    private CommandWrapper GenerateCommand()
    {
        CommandWrapper command = new();
        command.SetCommandText(_command);
        return command;
    }
}
