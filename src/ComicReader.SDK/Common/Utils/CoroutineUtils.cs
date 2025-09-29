// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Common.Utils;

public static class CoroutineUtils
{
    public static void Start(Func<Task> task)
    {
        _ = task();
    }

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
