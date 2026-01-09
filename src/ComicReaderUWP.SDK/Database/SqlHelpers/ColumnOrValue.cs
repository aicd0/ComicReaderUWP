// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.SDK.Database.SqlHelpers;

public class ColumnOrValue
{
    public readonly IColumnTypeless? Column;
    public readonly object? Value;
    private bool _collateNocase = false;

    private ColumnOrValue(IColumnTypeless? column, object? value)
    {
        Column = column;
        Value = value;
    }

    public ColumnOrValue CollateNocase()
    {
        _collateNocase = true;
        return this;
    }

    internal void AppendToCommand(StringBuilder sb, ICommandContext command)
    {
        if (Column is null)
        {
            if (Value is null)
            {
                sb.Append("NULL");
            }
            else
            {
                string parameterName = command.AppendParameter(Value);
                sb.Append(parameterName);
            }
        }
        else
        {
            sb.Append(Column.Name);
            if (_collateNocase)
            {
                sb.Append(" COLLATE NOCASE");
            }
        }
    }

    public static ColumnOrValue FromColumn(IColumnTypeless column)
    {
        return new ColumnOrValue(column, null);
    }

    public static ColumnOrValue FromValue(object? value)
    {
        return new ColumnOrValue(null, value);
    }
}
