// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Threading;

public static class ThreadingUtils
{
    public static async Task<R> Submit<R>(ITaskDispatcher dispatcher, string taskName, Func<R> action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher, nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(taskName, nameof(taskName));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        var completionSrc = new TaskCompletionSource<R>();
        dispatcher.Submit(taskName, () =>
        {
            completionSrc.SetResult(action());
        });
        return await completionSrc.Task;
    }
}
