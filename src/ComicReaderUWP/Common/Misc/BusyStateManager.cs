// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.Common.Misc;

internal static class BusyStateManager
{
    private static readonly MutableLiveData<bool> _busy = new(false);
    public static ILiveData<bool> Busy => _busy;

    private static volatile int _busyCount = 0;

    public static async Task WithBusyState(Func<Task> action)
    {
        Task task = action();
        try
        {
            await task.WaitAsync(TimeSpan.FromMilliseconds(500));
        }
        catch (TimeoutException)
        {
        }

        if (task.IsCompleted)
        {
            return;
        }

        int newCount = Interlocked.Increment(ref _busyCount);
        if (newCount == 1)
        {
            DispatchBusyStateChanged();
        }

        try
        {
            await task;
        }
        finally
        {
            newCount = Interlocked.Decrement(ref _busyCount);
            if (newCount == 0)
            {
                DispatchBusyStateChanged();
            }
        }
    }

    private static void DispatchBusyStateChanged()
    {
        CoroutineUtils.PostInMainThread(() =>
        {
            _busy.Emit(_busyCount > 0);
        });
    }
}
