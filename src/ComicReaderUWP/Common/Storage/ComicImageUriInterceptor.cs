// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

using ComicReaderUWP.Helpers.Imaging;

namespace ComicReaderUWP.Common.Storage;

internal sealed class ComicImageUriInterceptor : IResourceUriInterceptor
{
    private const string HOST = "ComicImage";
    private const string QUERY_COMIC_ID = "id";
    private const string QUERY_INDEX = "index";

    public bool TryParse(Uri uri, [NotNullWhen(true)] out IResourceUriHandler? handler)
    {
        handler = null;
        if (!uri.Host.Equals(HOST, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string comicIdRaw = string.Empty;
        string indexRaw = string.Empty;
        foreach (string item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equalsIndex = item.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string key = item[..equalsIndex];
            string itemValue = item[(equalsIndex + 1)..];
            if (key.Equals(QUERY_COMIC_ID, StringComparison.OrdinalIgnoreCase))
            {
                comicIdRaw = itemValue;
            }
            else if (key.Equals(QUERY_INDEX, StringComparison.OrdinalIgnoreCase))
            {
                indexRaw = itemValue;
            }
        }

        if (!long.TryParse(comicIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long comicId))
        {
            return false;
        }

        if (!int.TryParse(indexRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) || index < 0)
        {
            return false;
        }

        handler = new Handler(comicId, index);
        return true;
    }

    public sealed class Handler : IResourceUriHandler
    {
        private readonly Uri _uri;

        public Handler(long comicId, int index)
        {
            ComicId = comicId;
            Index = index;
            string idText = comicId.ToString(CultureInfo.InvariantCulture);
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            _uri = new Uri($"{ResourceUri.SCHEME}{HOST}?{QUERY_COMIC_ID}={idText}&{QUERY_INDEX}={indexText}");
        }

        public long ComicId { get; }

        public int Index { get; }

        public Uri Uri => _uri;

        public async Task<IImageSource?> ResolveImage()
        {
            ComicModel? comic = await ComicModel.FromId(ComicId);
            if (comic is null)
            {
                return null;
            }

            return new ComicImageSource(comic, Index, _uri.ToString());
        }

        public Task Release()
        {
            return Task.CompletedTask;
        }
    }
}
