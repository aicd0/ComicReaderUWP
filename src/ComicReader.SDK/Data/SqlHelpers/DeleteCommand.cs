// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

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

        StringBuilder sb = new("DELETE FROM ");
        sb.Append(_table.GetTableName());

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
