// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Common.ErrorHandling;

internal partial class ErrorLogger<T> : IErrorLogger where T : notnull
{
    private const string TAG = nameof(ErrorLogger<>);

    public static ErrorLogger<T> Create(string tag = TAG)
    {
        return new(tag);
    }

    private int _isResultSet = 0;
    private readonly string _tag;
    private ImmutableList<IErrorLogger> _children = [];

    public T? Result { get; private set; } = default;
    public bool IsSuccessful { get; private set; } = true;
    public string Message { get; private set; } = string.Empty;
    public Exception? Exception { get; private set; }
    public bool IsFatal { get; private set; } = false;
    public IReadOnlyList<IErrorLogger> Children => Volatile.Read(ref _children);

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

    private ErrorLogger(string tag)
    {
        _tag = tag;
    }

    public void Attach(IErrorLogger err)
    {
        ImmutableInterlocked.Update(ref _children, list => list.Add(err));
    }

    public void CopyErrorTo(IErrorLogger err)
    {
        if (IsSuccessful)
        {
            return;
        }

        err.SetError(Message, Exception, IsFatal);
    }

    public void SetResult()
    {
        SetResult(default);
    }

    public void SetResult(T? result)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            Logger.F(TAG, "The result is already set.");
            return;
        }

        Result = result;
        IsSuccessful = true;
        Message = string.Empty;
        Exception = null;
        IsFatal = false;
    }

    public void SetError(Exception exception, bool isFatal = false)
    {
        SetError(exception.Message, exception, isFatal);
    }

    public void SetError(string message, Exception? exception = null, bool isFatal = false)
    {
        if (Interlocked.Exchange(ref _isResultSet, 1) == 1)
        {
            Logger.F(TAG, "The result is already set.");
            return;
        }

        Result = default;
        IsSuccessful = false;
        Message = message;
        Exception = exception;
        IsFatal = isFatal;

        if (isFatal)
        {
            Logger.F(_tag, message, exception);
        }
        else
        {
            Logger.E(_tag, message, exception);
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
