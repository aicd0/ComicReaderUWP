// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Windows.Storage.Streams;

namespace ComicReaderUWP.SDK.Common.Caching;

public interface ILRUInputStream : IDisposable
{
    Task WriteAsync(IBuffer buffer);
}
