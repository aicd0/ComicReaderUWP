// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class InsertCommand
{
    private readonly ITable _table;
    private readonly Dictionary<string, Token> _tokens = [];
    private readonly List<UpsertOperation> _upsertOps = [];

    private bool _executed = false;

    private InsertCommand(ITable table)
    {
        _table = table;
    }

    public static InsertCommand Create(ITable table)
    {
        return new(table);
    }

    public InsertCommand AppendColumn(IColumnTypeless column, object value)
    {
        var token = new Token(column, value);
        _tokens[column.Name] = token;
        return this;
    }

    public OnConflictBuilder OnConflict(IEnumerable<IColumnTypeless> columns)
    {
        return new(this, columns);
    }

    public long Execute()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }
        _executed = true;

        if (_tokens.Count == 0)
        {
            throw new ArgumentException("Empty tokens.");
        }

        CommandWrapper command = new();

        StringBuilder sb = new("INSERT INTO ");
        sb.Append(_table.GetTableName());
        sb.Append(" (");

        {
            bool divider = false;
            foreach (Token token in _tokens.Values)
            {
                if (divider)
                {
                    sb.Append(',');
                }

                divider = true;
                sb.Append(token.GetColumnName());
            }
        }

        sb.Append(") VALUES (");

        {
            bool divider = false;
            foreach (Token token in _tokens.Values)
            {
                string parameterName = token.AppendParameter(command);
                if (divider)
                {
                    sb.Append(',');
                }

                divider = true;
                sb.Append(parameterName);
            }
        }

        sb.Append(')');

        foreach (UpsertOperation op in _upsertOps)
        {
            sb.Append(" ON CONFLICT(");
            {
                bool divider = false;
                foreach (IColumnTypeless column in op.Columns)
                {
                    if (divider)
                    {
                        sb.Append(',');
                    }

                    divider = true;
                    sb.Append(column.Name);
                }
            }

            sb.Append(") DO UPDATE SET ");
            {
                bool divider = false;
                foreach (Token token in op.Tokens.Values)
                {
                    string parameterName = token.AppendParameter(command);
                    if (divider)
                    {
                        sb.Append(", ");
                    }

                    divider = true;
                    sb.Append(token.GetColumnName()).Append(" = ").Append(parameterName);
                }
            }
        }

        sb.Append("; SELECT LAST_INSERT_ROWID();");

        command.SetCommandText(sb.ToString());
        return (long)command.ExecuteScalar(_table.GetDatabase())!;
    }

    private class Token(IColumnTypeless column, object value)
    {
        private readonly IColumnTypeless _column = column;
        private readonly object _value = value;

        public string AppendParameter(ICommandContext command)
        {
            return command.AppendParameter(_value);
        }

        public string GetColumnName()
        {
            return _column.Name;
        }
    }

    private class UpsertOperation(List<IColumnTypeless> columns, Dictionary<string, Token> tokens)
    {
        public readonly List<IColumnTypeless> Columns = columns;
        public readonly Dictionary<string, Token> Tokens = new(tokens);
    }

    public class OnConflictBuilder(InsertCommand command, IEnumerable<IColumnTypeless> columns)
    {
        private readonly InsertCommand _command = command;
        private readonly List<IColumnTypeless> _columns = [.. columns];
        private readonly Dictionary<string, Token> _tokens = [];

        public OnConflictBuilder Update(IColumnTypeless column, object value)
        {
            _tokens[column.Name] = new(column, value);
            return this;
        }

        public InsertCommand End()
        {
            if (_columns.Count > 0 && _tokens.Count > 0)
            {
                _command._upsertOps.Add(new(_columns, _tokens));
            }

            return _command;
        }
    }
}
