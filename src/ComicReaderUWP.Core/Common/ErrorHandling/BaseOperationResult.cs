// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ComicReaderUWP.Core.Common.ErrorHandling;

public abstract partial class BaseOperationResult(bool isSuccessful)
{
    public bool IsSuccessful { get; } = isSuccessful;
    public required string Message { get; init; }
    public required Exception? Exception { get; init; }
    public required bool IsFatal { get; init; }

    public string ErrorMessage
    {
        get
        {
            if (IsSuccessful)
            {
                return string.Empty;
            }

            return GetErrorMessage();
        }
    }

    private string GetErrorMessage()
    {
        string commonMessage = NormalizeMessage(Message);
        string exceptionMessage = NormalizeMessage(Exception?.Message);

        string message;
        if (string.IsNullOrEmpty(commonMessage))
        {
            message = exceptionMessage;
        }
        else if (string.IsNullOrEmpty(exceptionMessage))
        {
            message = commonMessage;
        }
        else
        {
            message = $"{commonMessage}\n{exceptionMessage}";
        }

        if (string.IsNullOrEmpty(message))
        {
            message = "(no message)";
        }

        return message;
    }

    private static string NormalizeMessage(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        input = input.Trim('\r', '\n');
        return LineBreakRegex().Replace(input, " ");
    }

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreakRegex();
}
