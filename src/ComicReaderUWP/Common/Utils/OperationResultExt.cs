// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ErrorHandling;

namespace ComicReaderUWP.Common.Utils;

internal static class OperationResultExt
{
    private const string TAG = nameof(OperationResultExt);

    public static OperationResult Success()
    {
        return OperationResult.Success();
    }

    public static OperationResult Failure(string tag, Exception exception, bool isFatal = false)
    {
        return Failure(tag, exception.Message, exception, isFatal);
    }

    public static OperationResult Failure(string tag, string context, BaseOperationResult inner)
    {
        return Failure(tag, $"{context}: {inner.Message}", inner.Exception, inner.IsFatal);
    }

    public static OperationResult Failure(string tag, string message, Exception? exception = null, bool isFatal = false)
    {
        Log(tag, message, exception, isFatal);
        return OperationResult.Failure(message, exception, isFatal);
    }

    public static OperationResult Failure(BaseOperationResult inner)
    {
        Log(TAG, inner.Message, inner.Exception, inner.IsFatal);
        return OperationResult.Failure(inner);
    }

    public static void ShowErrorMessage(this BaseOperationResult result, ActionHandler actionHandler)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
            .AddParameter(MessageDialogProvider.PARAM_TITLE, StringResourceProvider.Instance.Error)
            .AddParameter(MessageDialogProvider.PARAM_MESSAGE, StringResourceProvider.Instance.ErrorCommonDescription + "\n" + result.ErrorMessage)
            .Build();
        actionHandler.HandleNoResult(actionModel);
    }

    private static void Log(string tag, string message, Exception? exception, bool isFatal)
    {
        if (isFatal)
        {
            Logger.F(tag, message, exception);
        }
        else
        {
            Logger.E(tag, message, exception);
        }
    }
}

internal static class OperationResultExt<T>
{
    private const string TAG = nameof(OperationResultExt<>);

    public static OperationResult<T> Success(T result)
    {
        return new OperationResult<T>(result)
        {
            Message = string.Empty,
            Exception = null,
            IsFatal = false,
        };
    }

    public static OperationResult<T> Failure(string tag, Exception exception, bool isFatal = false)
    {
        return Failure(tag, exception.Message, exception, isFatal);
    }

    public static OperationResult<T> Failure(string tag, string message, Exception? exception = null, bool isFatal = false)
    {
        Log(tag, message, exception, isFatal);
        return new OperationResult<T>()
        {
            Message = message,
            Exception = exception,
            IsFatal = isFatal,
        };
    }

    public static OperationResult<T> Failure(BaseOperationResult inner)
    {
        Log(TAG, inner.Message, inner.Exception, inner.IsFatal);
        return new OperationResult<T>()
        {
            Message = inner.Message,
            Exception = inner.Exception,
            IsFatal = inner.IsFatal,
        };
    }

    private static void Log(string tag, string message, Exception? exception, bool isFatal)
    {
        if (isFatal)
        {
            Logger.F(tag, message, exception);
        }
        else
        {
            Logger.E(tag, message, exception);
        }
    }
}
