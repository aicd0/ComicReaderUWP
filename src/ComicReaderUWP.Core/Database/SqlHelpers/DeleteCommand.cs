// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public class DeleteCommand
{
    private readonly ITable _table;
    private readonly List<ICondition> _conditions = [];

    private bool _executed = false;

    private DeleteCommand(ITable table)
    {
        _table = table;
    }

    public static DeleteCommand Create(ITable table)
    {
        return new(table);
    }

    public DeleteCommand AppendCondition(IColumnTypeless column, object value)
    {
        return AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(value)));
    }

    public DeleteCommand AppendCondition(ICondition condition)
    {
        _conditions.Add(condition);
        return this;
    }

    public int Execute()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        CommandWrapper command = GenerateCommand();
        return command.ExecuteNonQuery(_table.Database);
    }

    public async Task ExecuteAsync()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        CommandWrapper command = GenerateCommand();
        await command.ExecuteNonQueryAsync(_table.Database);
    }

    private CommandWrapper GenerateCommand()
    {
        CommandWrapper command = new();

        StringBuilder sb = new("DELETE FROM ");
        sb.Append(_table.TableName);

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" AND ");
                }
                sb.Append('(').Append(_conditions[i].GetExpression(command)).Append(')');
            }
        }

        command.SetCommandText(sb.ToString());
        return command;
    }
}
