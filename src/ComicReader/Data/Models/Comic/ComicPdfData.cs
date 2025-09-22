// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Pdf;
using ComicReader.SDK.Common.Utils;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal class ComicPdfData : ComicData
{
    private const string TAG = nameof(ComicPdfData);

    private StorageFile? _pdfFile;

    public override bool IsEditable => !IsExternal;

    private ComicPdfData(bool is_external) :
        base(ComicType.PDF, is_external)
    { }

    public static ComicData FromDatabase(string location)
    {
        return new ComicPdfData(false)
        {
            Location = location,
        };
    }

    public static async Task<ComicData> FromExternal(StorageFile file)
    {
        var comic = new ComicPdfData(true)
        {
            Title1 = file.DisplayName,
            Location = file.Path,
        };

        await comic.ReloadImageFiles();
        return comic;
    }

    private async Task<StorageFile?> GetFile()
    {
        StorageFile? file = _pdfFile;
        if (file != null)
        {
            return file;
        }

        if (string.IsNullOrEmpty(Location))
        {
            return null;
        }

        string basePath = ArchiveAccess.GetBasePath(Location, false);
        file = await Storage.TryGetFile(basePath);
        if (file == null)
        {
            return null;
        }

        _pdfFile = file;
        return file;
    }

    protected override async Task<bool> ReloadImages()
    {
        StorageFile? file = await GetFile();
        if (file is null)
        {
            return false;
        }

        return true;
    }

    public override string GetImageCacheKey(int index)
    {
        StorageFile? file = _pdfFile;
        if (file == null)
        {
            return string.Empty;
        }

        return file.Path + ":" + index.ToString();
    }

    public override int GetImageSignature(int index)
    {
        return FileUtils.GetFileHashCode(_pdfFile);
    }

    protected override async Task<IComicConnection?> OpenComicConnection()
    {
        StorageFile? file = await GetFile();
        if (file is null)
        {
            return null;
        }

        PdfManager.IPdfConnection? connection = await PdfManager.OpenPdf(file.Path, null);
        if (connection == null)
        {
            return null;
        }

        return new PdfComicConnection(connection);
    }

    private class PdfComicConnection : IComicConnection
    {
        private readonly PdfManager.IPdfConnection _connection;

        public PdfComicConnection(PdfManager.IPdfConnection connection)
        {
            _connection = connection;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public int GetImageCount()
        {
            return _connection.GetPageCount();
        }

        public string GetImageName(int index)
        {
            return StringResourceProvider.Instance.PageN.Replace("$page", (index + 1).ToString());
        }

        public async Task<IRandomAccessStream?> GetImageStream(int index)
        {
            MemoryStream? memoryStream = new();
            try
            {
                SizeF size = _connection.GetPageSize(index);
                CalculatePageSize(size.Width, size.Height, out int width, out int height);
                using Image? image = await _connection.Render(index, width, height);
                if (image == null)
                {
                    return null;
                }
                image.Save(memoryStream, ImageFormat.Png);
            }
            catch (Exception e)
            {
                Logger.F(TAG, "GetImageStream", e);
                memoryStream.Dispose();
                memoryStream = null;
            }
            return memoryStream?.AsRandomAccessStream();
        }

        private void CalculatePageSize(float originWidth, float originHeight, out int width, out int height)
        {
            int defaultWidth = 764;
            int defaultHeight = 1080;

            if (!(float.IsFinite(originWidth) && float.IsFinite(originHeight) && originWidth > 0 && originHeight > 0))
            {
                width = defaultWidth;
                height = defaultHeight;
                return;
            }

            DisplayUtils.GetScreenSize(out int screenWidth, out int screenHeight);
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                width = (int)originWidth;
                height = (int)originHeight;
                return;
            }

            float pageAspectRatio = originWidth / originHeight;
            float screenAspectRatio = (float)screenWidth / screenHeight;

            float targetWidth, targetHeight;
            if (pageAspectRatio > screenAspectRatio)
            {
                targetHeight = screenHeight;
                targetWidth = screenHeight / originHeight * originWidth;
            }
            else
            {
                targetWidth = screenWidth;
                targetHeight = screenWidth / originWidth * originHeight;
            }

            float maxResolution = 10000000;
            float targetResolution = targetWidth * targetHeight;
            if (targetResolution > maxResolution)
            {
                float dimensionFactor = (float)Math.Sqrt(maxResolution / targetResolution);
                targetWidth *= dimensionFactor;
                targetHeight *= dimensionFactor;
            }
            width = (int)targetWidth;
            height = (int)targetHeight;
        }
    }
}
