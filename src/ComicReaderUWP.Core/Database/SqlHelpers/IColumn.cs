// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public interface IColumnTypeless
{
    string Name { get; }
}

public interface IColumn<T> : IColumnTypeless
{
    IReaderToken<T> PutQuery(SelectCommand command);
}

public class Int32Column(string name) : IColumn<int>
{
    public string Name => name;

    public IReaderToken<int> PutQuery(SelectCommand command)
    {
        return command.PutQueryInt32(this);
    }
}

public class Int64Column(string name) : IColumn<long>
{
    public string Name => name;

    public IReaderToken<long> PutQuery(SelectCommand command)
    {
        return command.PutQueryInt64(this);
    }
}

public class StringColumn(string name) : IColumn<string>
{
    public string Name => name;

    public IReaderToken<string> PutQuery(SelectCommand command)
    {
        return command.PutQueryString(this);
    }
}

public class BooleanColumn(string name) : IColumn<bool>
{
    public string Name => name;

    public IReaderToken<bool> PutQuery(SelectCommand command)
    {
        return command.PutQueryBoolean(this);
    }
}

public class DoubleColumn(string name) : IColumn<double>
{
    public string Name => name;

    public IReaderToken<double> PutQuery(SelectCommand command)
    {
        return command.PutQueryDouble(this);
    }
}

public class DateTimeOffsetColumn(string name) : IColumn<DateTimeOffset>
{
    public string Name => name;

    public IReaderToken<DateTimeOffset> PutQuery(SelectCommand command)
    {
        return command.PutQueryDateTimeOffset(this);
    }
}
