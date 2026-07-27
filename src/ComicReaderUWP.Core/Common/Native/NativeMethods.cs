// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace ComicReaderUWP.Core.Common.Native;

internal partial class NativeMethods
{
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint eventHookAssemblyHandle, NativeModels.WinEventDelegate eventHookHandle, uint processId, uint threadId, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);
}
