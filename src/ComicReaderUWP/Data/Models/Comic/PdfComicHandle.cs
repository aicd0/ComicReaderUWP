// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Pdf;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.Data.Models.Comic;

internal partial class PdfComicHandle : ComicHandle
{
    private const string TAG = nameof(PdfComicHandle);

    public static ComicHandle FromExternal(string path)
    {
        return new PdfComicHandle()
        {
            Location = path,
            Title1 = Path.GetFileNameWithoutExtension(path),
        };
    }

    public override bool IsEditable => !IsExternal;

    protected override ComicType Type => ComicType.PDF;

    public override IReadOnlyList<string> GetFolderViewPath()
    {
        string? dirName = Path.GetDirectoryName(Location);
        if (string.IsNullOrEmpty(dirName))
        {
            return [];
        }

        return dirName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    protected override async Task<BaseComicConnection?> OpenComicConnection()
    {
        PdfManager.IPdfConnection? connection = await PdfManager.OpenPdf(Location, null);
        if (connection is null)
        {
            return null;
        }

        if (connection.GetPageCount() == 0)
        {
            connection.Dispose();
            return null;
        }

        return new PdfComicConnection(Location, connection);
    }

    private static MemoryStream CreateStreamFromBuffer(nint buffer, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(stride);

        if (buffer == nint.Zero)
        {
            throw new ArgumentException("Buffer pointer is null.", nameof(buffer));
        }

        var stream = new MemoryStream();
        try
        {
            using var bitmap = new Bitmap(
                width,
                height,
                stride,
                PixelFormat.Format32bppPArgb,
                buffer);
            try
            {
                bitmap.Save(stream, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, $"CreateStreamFromBuffer#Save (W={width},H={height},S={stride})", ex);
                throw;
            }

            stream.Position = 0;
        }
        catch (Exception)
        {
            stream.Dispose();
            throw;
        }

        return stream;
    }

    private partial class PdfComicConnection(string pdfPath, PdfManager.IPdfConnection connection) : BaseComicConnection
    {
        public override int ImageCount => connection.GetPageCount();

        public override void Dispose()
        {
            connection.Dispose();
        }

        public override string GetImageCacheKey(int index)
        {
            return pdfPath + ":" + index.ToString();
        }

        public override string GetImageSignature(int index)
        {
            return FileUtils.GetFileSignature(pdfPath);
        }

        public override async Task<Stream?> OpenImageStream(int index)
        {
            SizeF size = connection.GetPageSize(index);
            int width = (int)Math.Round(size.Width);
            int height = (int)Math.Round(size.Height);
            return await connection.Render(index, width, height, (buffer, stride) =>
            {
                return CreateStreamFromBuffer(buffer, width, height, stride);
            });
        }

        public override IVectorImageService? OpenVectorService(int index)
        {
            return new VectorService(connection.Clone(), index);
        }
    }

    private partial class VectorService(PdfManager.IPdfConnection connection, int index) : IVectorImageService
    {
        public void Dispose()
        {
            connection.Dispose();
        }

        public Task<Windows.Graphics.Imaging.SoftwareBitmap?> CreateSoftwareBitmap(int width, int height)
        {
            return Render(index, width, height, buffer =>
            {
                try
                {
                    return Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
                        buffer.AsBuffer(),
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        width, height);
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                    return null;
                }
            });
        }

        public Task<CanvasBitmap?> CreateImageCanvasBitmap(ICanvasResourceCreator creator, int width, int height)
        {
            return Render(index, width, height, buffer =>
            {
                try
                {
                    return CanvasBitmap.CreateFromBytes(
                        creator,
                        buffer,
                        width,
                        height,
                        Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                    return null;
                }
            });
        }

        private async Task<T?> Render<T>(int index, int width, int height, Func<byte[], T?> func)
        {
            int bytesPerPixel = 4;
            int rowBytes = width * bytesPerPixel;
            int totalBytes = rowBytes * height;
            byte[] packed = ArrayPool<byte>.Shared.Rent(totalBytes);
            try
            {
                bool success = await connection.Render(index, width, height, (buffer, stride) =>
                {
                    unsafe
                    {
                        byte* src = (byte*)buffer;
                        fixed (byte* dstBase = packed)
                        {
                            byte* dst = dstBase;
                            for (int y = 0; y < height; y++)
                            {
                                Buffer.MemoryCopy(
                                    src + y * stride,
                                    dst + y * rowBytes,
                                    rowBytes,
                                    rowBytes);
                            }
                        }
                    }

                    return true;
                });

                if (!success)
                {
                    return default;
                }

                return func(packed);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(packed);
            }
        }
    }
}
