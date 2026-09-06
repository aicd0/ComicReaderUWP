// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReaderUWP.Common.ErrorHandling;

internal sealed partial class ErrorResult<T> : BaseErrorResult
{
    private readonly T _result;

    public T Result => IsSuccessful ? _result : throw new InvalidOperationException("Result is not available.");

    public ErrorResult() : base(false)
    {
        _result = default!;
    }

    public ErrorResult(T result) : base(true)
    {
        _result = result;
    }
}

internal sealed class ErrorResult(bool isSuccessful) : BaseErrorResult(isSuccessful)
{
}
