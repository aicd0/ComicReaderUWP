// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace ComicReader.SDK.Common.Native;

public class NativeMethods
{
    // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-createfilefromappw
    [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileFromApp(string lpFileName,
        int dwDesiredAccess,
        int dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        SafeFileHandle hTemplateFile
    );

    // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-findfirstfileexfromappw
    [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint FindFirstFileExFromApp(
        string lpFileName,
        NativeModels.FindExInfoLevel fInfoLevelId,
        out NativeModels.Win32FindData lpFindFileData,
        NativeModels.FIndexSearchOps fSearchOp,
        nint lpSearchFilter,
        int dwAdditionalFlags
    );

    [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
    public static extern bool FindNextFile(
        nint hFindFile,
        out NativeModels.Win32FindData lpFindFileData
    );

    [DllImport("api-ms-win-core-file-l1-1-0.dll")]
    public static extern bool FindClose(nint hFindFile);

    [DllImport("Shcore.dll", SetLastError = true)]
    public static extern int GetDpiForMonitor(nint hmonitor, NativeModels.MonitorDPIType dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint eventHookAssemblyHandle, NativeModels.WinEventDelegate eventHookHandle, uint processId, uint threadId, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("gdi32.dll")]
    public static extern int GetDeviceCaps(nint hdc, int nIndex);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);
}
