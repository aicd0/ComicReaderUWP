// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Data.Sqlite;

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public partial class SelectCommand
{
    private readonly ITable _table;
    private readonly Dictionary<string, ITokenInternal> _tokens = [];
    private readonly List<ICondition> _conditions = [];

    private bool _distinct = false;
    private int _limit = 0;
    private bool _executed = false;

    private SelectCommand(ITable table)
    {
        _table = table;
    }

    public static SelectCommand Create(ITable table)
    {
        return new(table);
    }

    public SelectCommand Distinct()
    {
        _distinct = true;
        return this;
    }

    public SelectCommand Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public IReaderToken<long> PutQueryCountAll()
    {
        return PutToken(new GeneralToken<long>("COUNT(*)", delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt64(ordinal); }));
    }

    public IReaderToken<int> PutQueryInt32(IColumn<int> column)
    {
        return PutToken(new ColumnToken<int>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt32(ordinal); }));
    }

    public IReaderToken<long> PutQueryInt64(IColumn<long> column)
    {
        return PutToken(new ColumnToken<long>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetInt64(ordinal); }));
    }

    public IReaderToken<string> PutQueryString(IColumn<string> column)
    {
        return PutToken(new ColumnToken<string>(column, delegate (SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }));
    }

    public IReaderToken<bool> PutQueryBoolean(IColumn<bool> column)
    {
        return PutToken(new ColumnToken<bool>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetBoolean(ordinal); }));
    }

    public IReaderToken<double> PutQueryDouble(IColumn<double> column)
    {
        return PutToken(new ColumnToken<double>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDouble(ordinal); }));
    }

    public IReaderToken<DateTimeOffset> PutQueryDateTimeOffset(IColumn<DateTimeOffset> column)
    {
        return PutToken(new ColumnToken<DateTimeOffset>(column, delegate (SqliteDataReader reader, int ordinal) { return reader.GetDateTimeOffset(ordinal); }));
    }

    public SelectCommand AppendCondition(IColumnTypeless column, object value)
    {
        return AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(value)));
    }

    public SelectCommand AppendCondition(ICondition condition)
    {
        _conditions.Add(condition);
        return this;
    }

    public IReader Execute()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }

        _executed = true;
        var tokens = new List<ITokenInternal>(_tokens.Values);
        CommandWrapper command = GenerateCommand(tokens);
        return new Reader(command.ExecuteReader(_table.GetDatabase()), tokens);
    }

    public async Task<IReader> ExecuteAsync()
    {
        if (_executed)
        {
            throw new InvalidOperationException("Cannot execute the same command twice.");
        }

        _executed = true;
        var tokens = new List<ITokenInternal>(_tokens.Values);
        CommandWrapper command = GenerateCommand(tokens);
        return new Reader(await command.ExecuteReaderAsync(_table.GetDatabase()), tokens);
    }

    public override string ToString()
    {
        var tokens = new List<ITokenInternal>(_tokens.Values);
        CommandWrapper command = GenerateCommand(tokens);
        return command.ToString();
    }

    internal string ToSubquery(ICommandContext command)
    {
        var tokens = new List<ITokenInternal>(_tokens.Values);
        return GetQueryText(command, tokens);
    }

    private CommandWrapper GenerateCommand(List<ITokenInternal> tokens)
    {
        CommandWrapper command = new();
        string commandText = GetQueryText(command, tokens);
        command.SetCommandText(commandText);
        return command;
    }

    private string GetQueryText(ICommandContext command, List<ITokenInternal> tokens)
    {
        StringBuilder sb = new($"SELECT ");

        if (_distinct)
        {
            sb.Append("DISTINCT ");
        }

        if (tokens.Count == 0)
        {
            throw new InvalidOperationException("No tokens have been added to the select command.");
        }

        bool divider = false;
        foreach (ITokenInternal token in tokens)
        {
            if (divider)
            {
                sb.Append(',');
            }

            divider = true;
            sb.Append(token.GetQueryExpression());
        }

        sb.Append(" FROM ");
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

        if (_limit > 0)
        {
            sb.Append(" LIMIT ");
            sb.Append(_limit);
        }

        return sb.ToString();
    }

    private Token<V> PutToken<V>(Token<V> token)
    {
        _tokens[token.GetQueryExpression()] = token;
        return token;
    }

    private partial class Reader(SqliteDataReader reader, List<ITokenInternal> tokens) : IReader
    {
        public void Dispose()
        {
            reader?.Dispose();
        }

        public bool Read()
        {
            bool hasMore = reader.Read();
            if (hasMore)
            {
                for (int i = 0; i < tokens.Count; ++i)
                {
                    ITokenInternal token = tokens[i];
                    token.UpdateValue(reader, i);
                }
            }
            return hasMore;
        }

        public async Task<bool> ReadAsync()
        {
            bool hasMore = await reader.ReadAsync();
            if (hasMore)
            {
                for (int i = 0; i < tokens.Count; ++i)
                {
                    ITokenInternal token = tokens[i];
                    token.UpdateValue(reader, i);
                }
            }
            return hasMore;
        }
    }

    private abstract class Token<V> : IReaderToken<V>, ITokenInternal
    {
        private readonly Func<SqliteDataReader, int, V> _getter;
        private bool _valueSet = false;
        private V? _value = default;

        public Token(Func<SqliteDataReader, int, V> getter)
        {
            _getter = getter;
        }

        public abstract string GetQueryExpression();

        public V GetValue()
        {
            if (!_valueSet)
            {
                throw new InvalidOperationException("Value is not set.");
            }
            return _value!;
        }

        public void UpdateValue(SqliteDataReader reader, int ordinal)
        {
            _value = _getter(reader, ordinal);
            _valueSet = true;
        }
    }

    private class GeneralToken<V> : Token<V>
    {
        private readonly string _expression;

        public GeneralToken(string expression, Func<SqliteDataReader, int, V> getter) : base(getter)
        {
            _expression = expression;
        }

        public override string GetQueryExpression()
        {
            return _expression;
        }
    }

    private class ColumnToken<V> : Token<V>
    {
        private readonly IColumn<V> _column;

        public ColumnToken(IColumn<V> column, Func<SqliteDataReader, int, V> getter) : base(getter)
        {
            _column = column;
        }

        public override string GetQueryExpression()
        {
            return _column.Name;
        }
    }

    public interface IReader : IDisposable
    {
        bool Read();

        Task<bool> ReadAsync();
    }

    private interface ITokenInternal
    {
        string GetQueryExpression();

        void UpdateValue(SqliteDataReader reader, int ordinal);
    }
}
