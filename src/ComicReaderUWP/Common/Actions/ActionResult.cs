// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.Actions;

internal class ActionResult
{
    public required bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ActionResult FromSuccess()
    {
        return new()
        {
            Success = true,
        };
    }

    public static ActionResult FromFailure(string message)
    {
        return new()
        {
            Success = false,
            Message = message,
        };
    }
}
