// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common;

namespace ComicReader.Data.Models.TagInfo;

internal static class TagInfoDatabase
{
    static TagInfoDatabase()
    {
        TagCategoryInfoModel.InjectPrivateMembers();
        TagInfoModel.InjectPrivateMembers();
    }

    //
    // Internal methods, not intended for external use
    //

    public static Func<string, TagCategoryInfoModel> TagCategoryInfoGetOrCreateNoLock { get; set; } =
        (a) => throw new NullReferenceException("Method not injected.");
    public static Action<string> TagInfoDeleteTagCategoryCacheOnlyNoLock { get; set; } =
        (a) => throw new NullReferenceException("Method not injected.");
    public static Action<string, string> TagInfoRenameTagCategoryCacheOnlyNoLock { get; set; } =
        (a, b) => throw new NullReferenceException("Method not injected.");

    public static async Task<T> Enqueue<T>(string taskName, Func<T> op)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlDatabaseManager.TagInfoDatabaseDispatcher.Submit(taskName, delegate
        {
            taskResult.SetResult(op());
        });

        return await taskResult.Task;
    }

    public static void DispatchTagInfoUpdateEvents()
    {
        GlobalEvent.Instance.TagInfoUpdated.Emit(0);
    }
}
