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

internal partial class ErrorResult<T> : IErrorLogger where T : notnull
{
    private readonly T? _result;

    public T Result => _result ?? throw new NullReferenceException(nameof(_result));
    public required bool IsSuccessful { get; init; }
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

                string content = NormalizeLineBreaks(err.Message);
                if (string.IsNullOrEmpty(content))
                {
                    content = "(no message)";
                }

                StringBuilder sb = new();
                if (depth > 0)
                {
                    sb.Append(' ', depth * 2);
                    sb.Append("- ");
                }

                sb.Append(content);
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

    public ErrorResult(T? result)
    {
        _result = result;
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

    private static string NormalizeLineBreaks(string input)
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
