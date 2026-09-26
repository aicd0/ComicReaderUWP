// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;

using ComicReaderUWP.Helpers.Imaging;

namespace ComicReaderUWP.Common.Storage;

internal sealed class ResourceFileUriInterceptor : IResourceUriInterceptor
{
    private const string HOST = "Files";

    public bool TryParse(Uri uri, [NotNullWhen(true)] out IResourceUriHandler? handler)
    {
        handler = null;
        if (!uri.Host.Equals(HOST, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string path = uri.AbsolutePath;
        if (path.Length == 0 || path[0] != '/')
        {
            return false;
        }

        int fileNameIndex = path.IndexOf('/', 1);
        if (fileNameIndex <= 0 || fileNameIndex + 1 >= path.Length)
        {
            return false;
        }

        string resourceId = Uri.UnescapeDataString(path[1..fileNameIndex]);
        string fileName = Uri.UnescapeDataString(path[(fileNameIndex + 1)..]);
        if (!IsSafeSegment(resourceId) || !IsSafeSegment(fileName))
        {
            return false;
        }

        handler = new Handler(resourceId, fileName);
        return true;
    }

    private static bool IsSafeSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        if (segment is "." or "..")
        {
            return false;
        }

        foreach (char c in segment)
        {
            if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public sealed class Handler : IResourceUriHandler
    {
        private readonly string _resourceId;
        private readonly string _fileName;
        private readonly Uri _uri;

        public Handler(string resourceId, string fileName)
        {
            _resourceId = resourceId;
            _fileName = fileName;
            _uri = new Uri($"{ResourceUri.SCHEME}{HOST}/{resourceId}/{fileName}");
        }

        public Uri Uri => _uri;

        public async Task<IImageSource?> ResolveImage()
        {
            if (!ResourceManager.TryGetFilePath(_resourceId, _fileName, out string? filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return new LocalFileImageSource(filePath, _uri.ToString());
        }

        public Task Release()
        {
            return ResourceManager.DeleteFile(_resourceId, _fileName);
        }
    }
}
