// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ComicReaderUWP.Common.ErrorHandling;

internal interface IErrorLogger
{
    bool IsSuccessful { get; }

    string Message { get; }

    Exception? Exception { get; }

    bool IsFatal { get; }

    IReadOnlyList<IErrorLogger> Children { get; }
}
