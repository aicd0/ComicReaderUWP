// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Common;

public class InvalidExpressionException : Exception
{
    public InvalidExpressionException() : base() { }

    public InvalidExpressionException(string message) : base(message) { }

    public InvalidExpressionException(string message, Exception innerException) : base(message, innerException) { }
}
