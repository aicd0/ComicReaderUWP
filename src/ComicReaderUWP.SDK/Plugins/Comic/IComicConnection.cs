// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Comic;

public interface IComicConnection : IDisposable
{
    int ImageCount { get; }

    string GetImageName(int index);

    string GetImageSignature(int index);

    Task<Stream?> OpenImageStream(int index);
}
