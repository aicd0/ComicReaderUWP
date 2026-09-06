// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Common.ErrorHandling;

internal sealed partial class ErrorLogger<T> : BaseErrorLogger
{
    public static ErrorLogger<T> Create(string tag)
    {
        return new(tag);
    }

    public static ErrorResult<T> Run(string tag, Func<ErrorLogger<T>, ErrorResult<T>> func)
    {
        return func(Create(tag));
    }

    public static Task<ErrorResult<T>> Run(string tag, Func<ErrorLogger<T>, Task<ErrorResult<T>>> func)
    {
        return func(Create(tag));
    }

    private int _isResultSet = 0;
    private readonly string _tag;
    private ImmutableList<IErrorLogger> _children = [];

    private ErrorLogger(string tag)
    {
        _tag = tag;
    }

    public void Attach(IErrorLogger err)
    {
        ImmutableInterlocked.Update(ref _children, list => list.Add(err));
    }

    public ErrorResult<T> Success(T result)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            throw new InvalidOperationException("The result is already set.");
        }

        return new ErrorResult<T>(result)
        {
            Message = string.Empty,
            Exception = null,
            IsFatal = false,
            Children = Volatile.Read(ref _children),
        };
    }

    public ErrorResult<T> Error(IErrorLogger err)
    {
        if (err.IsSuccessful)
        {
            throw new InvalidOperationException("Cannot set a successful result as error.");
        }

        ImmutableInterlocked.Update(ref _children, list => [.. err.Children]);
        return Error(err.Message, err.Exception, err.IsFatal);
    }

    public ErrorResult<T> Error(Exception exception, bool isFatal = false)
    {
        return Error(exception.Message, exception, isFatal);
    }

    public ErrorResult<T> Error(string message, Exception? exception = null, bool isFatal = false)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            throw new InvalidOperationException("The result is already set.");
        }

        if (isFatal)
        {
            Logger.F(_tag, message, exception);
        }
        else
        {
            Logger.E(_tag, message, exception);
        }

        return new ErrorResult<T>()
        {
            Message = message,
            Exception = exception,
            IsFatal = isFatal,
            Children = Volatile.Read(ref _children),
        };
    }
}

internal sealed partial class ErrorLogger : BaseErrorLogger
{
    public static ErrorLogger Create(string tag)
    {
        return new(tag);
    }

    public static ErrorResult Run(string tag, Func<ErrorLogger, ErrorResult> func)
    {
        return func(Create(tag));
    }

    public static Task<ErrorResult> Run(string tag, Func<ErrorLogger, Task<ErrorResult>> func)
    {
        return func(Create(tag));
    }

    private int _isResultSet = 0;
    private readonly string _tag;
    private ImmutableList<IErrorLogger> _children = [];

    private ErrorLogger(string tag)
    {
        _tag = tag;
    }

    public void Attach(IErrorLogger err)
    {
        ImmutableInterlocked.Update(ref _children, list => list.Add(err));
    }

    public ErrorResult Success()
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            throw new InvalidOperationException("The result is already set.");
        }

        return new ErrorResult(true)
        {
            Message = string.Empty,
            Exception = null,
            IsFatal = false,
            Children = Volatile.Read(ref _children),
        };
    }

    public ErrorResult Error(IErrorLogger err)
    {
        if (err.IsSuccessful)
        {
            throw new InvalidOperationException("Cannot set a successful result as error.");
        }

        ImmutableInterlocked.Update(ref _children, list => [.. err.Children]);
        return Error(err.Message, err.Exception, err.IsFatal);
    }

    public ErrorResult Error(Exception exception, bool isFatal = false)
    {
        return Error(exception.Message, exception, isFatal);
    }

    public ErrorResult Error(string message, Exception? exception = null, bool isFatal = false)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            throw new InvalidOperationException("The result is already set.");
        }

        if (isFatal)
        {
            Logger.F(_tag, message, exception);
        }
        else
        {
            Logger.E(_tag, message, exception);
        }

        return new ErrorResult(false)
        {
            Message = message,
            Exception = exception,
            IsFatal = isFatal,
            Children = Volatile.Read(ref _children),
        };
    }
}
