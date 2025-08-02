// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class UpdateCommand
{
    private readonly ITable _table;
    private readonly Dictionary<string, IToken> _tokens = [];
    private readonly List<ICondition> _conditions = [];

    private bool _executed = false;

    private UpdateCommand(ITable table)
    {
        _table = table;
    }

    public static UpdateCommand Create(ITable table)
    {
        return new(table);
    }

    public UpdateCommand AppendColumn(IColumnTypeless column, object value)
    {
        var token = new Token(column, value);
        _tokens[column.Name] = token;
        return this;
    }

    public UpdateCommand AppendCondition(IColumnTypeless column, object value)
    {
        return AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(value)));
    }

    public UpdateCommand AppendCondition(ICondition condition)
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

        if (_tokens.Count == 0)
        {
            return;
        }

        CommandWrapper command = new();

        StringBuilder sb = new($"UPDATE {_table.GetTableName()} SET ");

        bool divider = false;
        foreach (IToken token in _tokens.Values)
        {
            string parameterKey = token.AppendParameter(command);

            if (divider)
            {
                sb.Append(',');
            }
            divider = true;
            sb.Append(token.GetColumnName());
            sb.Append('=');
            sb.Append(parameterKey);
        }

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
        command.ExecuteNonQuery(database);
    }

    private class Token : IToken
    {
        private readonly IColumnTypeless _column;
        private readonly object _value;

        public Token(IColumnTypeless column, object value)
        {
            _column = column;
            _value = value;
        }

        public string AppendParameter(ICommandContext command)
        {
            return command.AppendParameter(_value);
        }

        public string GetColumnName()
        {
            return _column.Name;
        }
    }

    private interface IToken
    {
        string GetColumnName();

        string AppendParameter(ICommandContext command);
    }
}
