// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace ComicReaderUWP.Core.Common.Pdf;

internal static partial class Pdfium
{
    private const string Dll = "pdfium.dll";

    [LibraryImport(Dll)]
    public static partial void FPDF_InitLibrary();

    [LibraryImport(Dll)]
    public static partial void FPDF_DestroyLibrary();

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint FPDF_LoadDocument(string filePath, string? password);

    [LibraryImport(Dll)]
    public static partial void FPDF_CloseDocument(nint doc);

    [LibraryImport(Dll)]
    public static partial int FPDF_GetPageCount(nint document);

    [LibraryImport(Dll)]
    public static partial nint FPDF_LoadPage(nint doc, int pageIndex);

    [LibraryImport(Dll)]
    public static partial void FPDF_ClosePage(nint page);

    [LibraryImport(Dll)]
    public static partial double FPDF_GetPageWidth(nint page);

    [LibraryImport(Dll)]
    public static partial double FPDF_GetPageHeight(nint page);

    [LibraryImport(Dll)]
    public static partial nint FPDFBitmap_Create(int width, int height, int alpha);

    [LibraryImport(Dll)]
    public static partial void FPDFBitmap_Destroy(nint bitmap);

    [LibraryImport(Dll)]
    public static partial nint FPDFBitmap_GetBuffer(nint bitmap);

    [LibraryImport(Dll)]
    public static partial int FPDFBitmap_GetStride(nint bitmap);

    [LibraryImport(Dll)]
    public static partial void FPDFBitmap_FillRect(
        nint bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(Dll)]
    public static partial void FPDF_RenderPageBitmap(
        nint bitmap,
        nint page,
        int startX,
        int startY,
        int sizeX,
        int sizeY,
        int rotate,
        int flags);
}
