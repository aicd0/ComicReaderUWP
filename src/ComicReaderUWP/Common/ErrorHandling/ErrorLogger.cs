// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Common.ErrorHandling;

internal partial class ErrorLogger<T> where T : notnull
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

    public ErrorResult<T> SetResult(T result)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            throw new InvalidOperationException("The result is already set.");
        }

        return new ErrorResult<T>(result)
        {
            IsSuccessful = true,
            Message = string.Empty,
            Exception = null,
            IsFatal = false,
            Children = Volatile.Read(ref _children),
        };
    }

    public ErrorResult<T> SetError(IErrorLogger err)
    {
        if (err.IsSuccessful)
        {
            throw new InvalidOperationException("Cannot set a successful result as error.");
        }

        ImmutableInterlocked.Update(ref _children, list => [.. err.Children]);
        return SetError(err.Message, err.Exception, err.IsFatal);
    }

    public ErrorResult<T> SetError(Exception exception, bool isFatal = false)
    {
        return SetError(exception.Message, exception, isFatal);
    }

    public ErrorResult<T> SetError(string message, Exception? exception = null, bool isFatal = false)
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

        return new ErrorResult<T>(default)
        {
            IsSuccessful = false,
            Message = message,
            Exception = exception,
            IsFatal = isFatal,
            Children = Volatile.Read(ref _children),
        };
    }
}
