// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace ComicReaderUWP.SDK.Common.Native;

public class NativeModels
{
    // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_info_levels
    public enum FindExInfoLevel
    {
        FindExInfoStandard = 0,
        FindExInfoBasic = 1
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_search_ops
    public enum FIndexSearchOps
    {
        FindExSearchNameMatch = 0,
        FindExSearchLimitToDirectories = 1,
        FindExSearchLimitToDevices = 2,
        FindExSearchMaxSearchOp = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct Win32FindData
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    public enum MonitorDPIType : int
    {
        MDT_Effective_DPI = 0,
        MDT_Angular_DPI = 1,
        MDT_Raw_DPI = 2,
        MDT_Default = MDT_Effective_DPI
    }

    public delegate void WinEventDelegate(nint winEventHookHandle, uint eventType, nint windowHandle, int objectId, int childId, uint eventThreadId, uint eventTimeInMilliseconds);
}
