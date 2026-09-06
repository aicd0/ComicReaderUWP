// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Localization;

namespace ComicReaderUWP.Common.ErrorHandling;

internal abstract partial class BaseErrorResult(bool isSuccessful) : IErrorLogger
{
    public bool IsSuccessful { get; } = isSuccessful;
    public required string Message { get; init; }
    public required Exception? Exception { get; init; }
    public required bool IsFatal { get; init; }
    public required IReadOnlyList<IErrorLogger> Children { get; init; }

    public string DetailedErrorMessage
    {
        get
        {
            if (IsSuccessful)
            {
                return string.Empty;
            }

            List<string> messages = [];

            void CollectMessages(int depth, IErrorLogger err)
            {
                if (err.IsSuccessful)
                {
                    return;
                }

                StringBuilder sb = new();
                if (depth > 0)
                {
                    sb.Append(' ', depth * 2);
                    sb.Append("- ");
                }

                sb.Append(GetErrorMessage());
                messages.Add(sb.ToString());

                IReadOnlyList<IErrorLogger> children = err.Children;
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    CollectMessages(depth + 1, children[i]);
                }
            }

            CollectMessages(0, this);
            return string.Join('\n', messages);
        }
    }

    public void DisplayErrorMessage(ActionHandler actionHandler)
    {
        if (IsSuccessful)
        {
            return;
        }

        string detailedMessage = DetailedErrorMessage;
        ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
            .AddParameter(MessageDialogProvider.PARAM_TITLE, StringResourceProvider.Instance.Error)
            .AddParameter(MessageDialogProvider.PARAM_MESSAGE, StringResourceProvider.Instance.ErrorCommonDescription + "\n" + detailedMessage)
            .Build();
        actionHandler.HandleNoResult(actionModel);
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
