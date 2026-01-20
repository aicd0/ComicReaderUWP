// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Drawing;

using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.SDK.Common.Pdf;

public static partial class PdfManager
{
    private const string TAG = nameof(PdfManager);

    private static readonly Lazy<IDisposableTaskDispatcher> _pdfQueue = new(() => TaskDispatcher.Factory.NewSingleThread("PdfQueue"));
    private static readonly object _documentLock = new();
    private static readonly Dictionary<string, PdfDocument> _documents = [];
    private static bool _libraryInitialized = false;

    public static async Task<IPdfConnection?> OpenPdf(string filepath, string? password)
    {
        string fullpath;
        try
        {
            fullpath = Path.GetFullPath(filepath);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        string key = fullpath;
        lock (_documentLock)
        {
            if (_documents.TryGetValue(key, out PdfDocument? existing))
            {
                existing.UseCount++;
                return new PdfConnection(existing);
            }
        }

        IPdfConnection? loadPdfFunc()
        {
            lock (_documentLock)
            {
                if (_documents.TryGetValue(key, out PdfDocument? existing))
                {
                    existing.UseCount++;
                    return new PdfConnection(existing);
                }
            }

            InitializeLibrary();
            nint docPtr = Pdfium.FPDF_LoadDocument(fullpath, password);
            if (docPtr == nint.Zero)
            {
                return null;
            }

            int pageCount = Pdfium.FPDF_GetPageCount(docPtr);
            List<SizeF> pageSizes = new(pageCount);
            for (int i = 0; i < pageCount; i++)
            {
                nint page = Pdfium.FPDF_LoadPage(docPtr, i);
                if (page == nint.Zero)
                {
                    pageSizes.Add(new SizeF());
                    continue;
                }

                double width = Pdfium.FPDF_GetPageWidth(page);
                double height = Pdfium.FPDF_GetPageHeight(page);
                Pdfium.FPDF_ClosePage(page);
                pageSizes.Add(new SizeF((float)width, (float)height));
            }

            PdfDocument document = new(key, docPtr, pageCount, pageSizes);
            lock (_documents)
            {
                _documents.Add(key, document);
            }

            return new PdfConnection(document);
        }

        return await Enqueue(loadPdfFunc);
    }

    private static void InitializeLibrary()
    {
        if (_libraryInitialized)
        {
            return;
        }

        Pdfium.FPDF_InitLibrary();
        _libraryInitialized = true;
    }

    private static async Task Enqueue(Action action)
    {
        var taskResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pdfQueue.Value.Submit(() =>
        {
            action();
            taskResult.SetResult(true);
        });
        await taskResult.Task;
    }

    private static async Task<T> Enqueue<T>(Func<T> action)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pdfQueue.Value.Submit(() =>
        {
            taskResult.SetResult(action());
        });
        return await taskResult.Task;
    }

    private partial class PdfDocument(
        string key,
        nint documentPtr,
        int pageCount,
        IReadOnlyList<SizeF> pageSizes) : IDisposable
    {
        public string Key { get; } = key;
        public int UseCount { get; set; } = 1;
        public nint DocumentPtr { get; private set; } = documentPtr;
        public int PageCount { get; } = pageCount;
        public IReadOnlyList<SizeF> PageSizes { get; } = pageSizes;

        private int _disposed = 0;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            CoroutineUtils.Start(() => Enqueue(() =>
            {
                Pdfium.FPDF_CloseDocument(DocumentPtr);
                DocumentPtr = nint.Zero;
            }));
        }
    }

    public interface IPdfConnection : IDisposable
    {
        IPdfConnection Clone();

        int GetPageCount();

        SizeF GetPageSize(int pageIndex);

        T? Render<T>(int pageIndex, int width, int height, Func<nint, int, T?> func);
    }

    private partial class PdfConnection(PdfDocument document) : IPdfConnection
    {
        public PdfDocument Document { get; } = document;

        private int _disposed = 0;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            bool needDispose;
            lock (_documentLock)
            {
                Document.UseCount--;
                needDispose = Document.UseCount == 0;
                if (needDispose)
                {
                    _documents.Remove(Document.Key);
                }
            }

            if (needDispose)
            {
                Document.Dispose();
            }
        }

        public IPdfConnection Clone()
        {
            lock (_documentLock)
            {
                ObjectDisposedException.ThrowIf(Document.UseCount <= 0, this);
                Document.UseCount++;
            }

            return new PdfConnection(Document);
        }

        public int GetPageCount()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return Document.PageCount;
        }

        public SizeF GetPageSize(int pageIndex)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return Document.PageSizes[pageIndex];
        }

        public T? Render<T>(int pageIndex, int width, int height, Func<nint, int, T?> func)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

            if (pageIndex < 0 || pageIndex >= Document.PageCount)
            {
                return default;
            }

            return Enqueue(() =>
            {
                nint bitmap = Pdfium.FPDFBitmap_Create(width, height, 1);
                if (bitmap == nint.Zero)
                {
                    return default;
                }

                try
                {
                    nint page = Pdfium.FPDF_LoadPage(Document.DocumentPtr, pageIndex);
                    if (page == nint.Zero)
                    {
                        return default;
                    }

                    try
                    {
                        Pdfium.FPDFBitmap_FillRect(bitmap, 0, 0, width, height, 0xFFFFFFFF);
                        Pdfium.FPDF_RenderPageBitmap(
                            bitmap,
                            page,
                            0, 0,
                            width, height,
                            0,
                            0);
                    }
                    finally
                    {
                        Pdfium.FPDF_ClosePage(page);
                    }

                    nint buffer = Pdfium.FPDFBitmap_GetBuffer(bitmap);
                    int stride = Pdfium.FPDFBitmap_GetStride(bitmap);
                    return func(buffer, stride);
                }
                finally
                {
                    Pdfium.FPDFBitmap_Destroy(bitmap);
                }
            }).Result;
        }
    }
}
