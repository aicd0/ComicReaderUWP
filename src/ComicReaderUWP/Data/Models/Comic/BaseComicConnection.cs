// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal abstract class BaseComicConnection : IComicConnection
{
    private const long SIGNATURE_CACHE_MS = 5000;

    private readonly ConcurrentDictionary<string, SignatureEntry> _signatures = [];

    public abstract int ImageCount { get; }

    public abstract void Dispose();

    public abstract string GetImageCacheKey(int index);

    public virtual string GetImagePath(int index)
    {
        return string.Empty;
    }

    public virtual string GetImageName(int index)
    {
        return string.Empty;
    }

    public abstract string GetImageSignature(int index);

    public abstract Task<Stream?> OpenImageStream(int index);

    public virtual IVectorImageService? OpenVectorService(int index)
    {
        return null;
    }

    protected string GetFileSignature(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        long now = Environment.TickCount64;
        if (_signatures.TryGetValue(path, out SignatureEntry entry) && now - entry.Tick < SIGNATURE_CACHE_MS)
        {
            return entry.Signature;
        }

        string signature = FileUtils.GetFileSignature(path);
        if (signature.Length > 0)
        {
            _signatures[path] = new SignatureEntry(signature, now);
        }

        return signature;
    }

    private readonly record struct SignatureEntry(string Signature, long Tick);
}
