// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common.Legacy;
using ComicReader.Common.Localization;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Pdf;
using ComicReader.SDK.Common.Utils;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal partial class PdfComicHandle : ComicHandle
{
    private const string TAG = nameof(PdfComicHandle);

    public static ComicHandle FromDatabase(string location)
    {
        return new PdfComicHandle(false)
        {
            Location = location,
        };
    }

    public static ComicHandle FromExternal(StorageFile file)
    {
        var comic = new PdfComicHandle(true)
        {
            Title1 = file.DisplayName,
            Location = file.Path,
        };

        return comic;
    }

    public override bool IsEditable => !IsExternal;

    private PdfComicHandle(bool is_external) : base(ComicType.PDF, is_external) { }

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

        return new PdfComicConnection(file.Path, connection);
    }

    private async Task<StorageFile?> GetFile()
    {
        if (string.IsNullOrEmpty(Location))
        {
            return null;
        }

        string basePath = ArchiveAccess.GetBasePath(Location, false);
        StorageFile? file = await Storage.TryGetFile(basePath);
        if (file is null)
        {
            return null;
        }

        return file;
    }

    private partial class PdfComicConnection(string pdfPath, PdfManager.IPdfConnection connection) : IComicConnection
    {
        public void Dispose()
        {
            connection.Dispose();
        }

        public int GetImageCount()
        {
            return connection.GetPageCount();
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
                SizeF size = connection.GetPageSize(index);
                CalculatePageSize(size.Width, size.Height, out int width, out int height);
                using Image? image = await connection.Render(index, width, height);
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

        public string GetImageCacheKey(int index)
        {
            return pdfPath + ":" + index.ToString();
        }

        public int GetImageSignature(int index)
        {
            return FileUtils.GetFileSignature(pdfPath);
        }

        private static void CalculatePageSize(float originWidth, float originHeight, out int width, out int height)
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
