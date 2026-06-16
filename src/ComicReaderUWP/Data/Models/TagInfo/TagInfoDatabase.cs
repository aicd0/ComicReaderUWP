// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Models.TagInfo;

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

    public static Task<T> Enqueue<T>(Func<T> op)
    {
        return SqliteDB.TagInfoDatabaseDispatcher.Submit(op);
    }

    public static void DispatchTagInfoUpdateEvents()
    {
        GlobalEvent.Instance.TagInfoUpdated.Emit(0);
    }
}
