// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.ErrorHandling;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Data.Models.Comic;

internal partial class CollectionComicHandle : ComicHandle
{
    private const string TAG = nameof(CollectionComicHandle);

    public static ComicHandle FromExternal()
    {
        return new CollectionComicHandle()
        {
            Location = string.Empty,
            Title1 = string.Empty,
        };
    }

    public override bool IsEditable => !IsExternal;

    protected override ComicType Type => ComicType.Collection;

    public override IReadOnlyList<string> GetFolderViewPath()
    {
        return [];
    }

    public override Task<OperationResult<BaseComicConnection>> OpenComicConnection()
    {
        return Task.FromResult(OperationResultExt<BaseComicConnection>.Failure(TAG, "A collection has no content to open."));
    }
}
