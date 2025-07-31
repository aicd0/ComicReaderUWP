// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.SDK.Common.Threading;

namespace ComicReader.Common.Utils;

internal static class CoroutineUtils
{
    public static Task<T> CreateTask<T>(string taskName, ITaskDispatcher dispatcher, Func<T> function)
    {
        TaskCompletionSource<T> completionSource = new();
        dispatcher.Submit(taskName, () =>
        {
            completionSource.SetResult(function());
        });
        return completionSource.Task;
    }
}
