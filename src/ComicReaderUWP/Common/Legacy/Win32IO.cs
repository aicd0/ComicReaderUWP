// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Native;

using Microsoft.Win32.SafeHandles;

namespace ComicReaderUWP.Common.Legacy;

public class Win32IO
{
    private const string TAG = "Win32IO";

    // System Error Codes
    // https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN
    internal const int ERROR_ACCESS_DENIED = 5;

    internal const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
    internal const int FIND_FIRST_EX_LARGE_FETCH = 2;
    internal const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

    internal const int GENERIC_READ = unchecked((int)0x80000000);
    internal const int GENERIC_ALL = unchecked(0x10000000);

    internal const int CREATE_NEW = 1;
    internal const int CREATE_ALWAYS = 2;
    internal const int OPEN_EXISTING = 3;
    internal const int OPEN_ALWAYS = 4;
    internal const int TRUNCATE_EXISTING = 5;

    internal const int FILE_ATTRIBUTE_NORMAL = 0x80;

    // SubFolderDeep
    public class SubItemDeepContext
    {
        public SubItemDeepContext(string path)
        {
            path = StringUtils.ToPathNoTail(path);

            m_stack.Add(new Node
            {
                Paths = new List<string> { path }
            });

            if (Environment.OSVersion.Version.Major >= 6)
            {
                FindInfoLevel = NativeModels.FindExInfoLevel.FindExInfoBasic;
                AdditionalFlags = FIND_FIRST_EX_LARGE_FETCH;
            }
            else
            {
                FindInfoLevel = NativeModels.FindExInfoLevel.FindExInfoStandard;
                AdditionalFlags = 0;
            }
        }

        private class Node
        {
            public List<string> Paths = new();
            public int Index = 0;

            public string CurrentPath => Paths[Index];
        }

        public List<string> Folders { get; private set; } = new List<string>();
        public List<string> Files { get; private set; } = new List<string>();
        public List<string> NoAccessFolders { get; private set; } = new List<string>();
        public int ItemFound => Folders.Count + Files.Count;

        private bool m_initial_search = true;
        private readonly List<Node> m_stack = new();
        private readonly NativeModels.FindExInfoLevel FindInfoLevel;
        private readonly NativeModels.FIndexSearchOps IndexSearchOps = NativeModels.FIndexSearchOps.FindExSearchNameMatch;
        private readonly int AdditionalFlags;

        public bool Search(uint item_count)
        {
            Folders.Clear();
            Files.Clear();
            NoAccessFolders.Clear();

            if (m_stack.Count == 0)
            {
                return false;
            }

            if (m_initial_search)
            {
                Logger.Assert(m_stack.Count == 1, "A870BE3C949DF1C3");
                m_initial_search = false;

                foreach (string path in m_stack[0].Paths)
                {
                    Folders.Add(path);
                }
            }

            bool not_end = InternalSearch(item_count);
            return ItemFound > 0 || not_end;
        }

        private bool InternalSearch(uint item_count, int depth = 0)
        {
            if (depth >= m_stack.Count)
            {
                // Visit current node.
                string path_raw = m_stack[m_stack.Count - 1].CurrentPath;
                string path = path_raw + "\\";
                nint h_file = NativeMethods.FindFirstFileExFromApp(path + "*", FindInfoLevel,
                    out _, IndexSearchOps, nint.Zero, AdditionalFlags);

                if (h_file.ToInt64() == -1)
                {
                    int error_code = Marshal.GetLastWin32Error();
                    Logger.I(TAG, "failed to access '" + path_raw + "', error code " + error_code.ToString());

                    if (error_code == ERROR_ACCESS_DENIED)
                    {
                        NoAccessFolders.Add(path_raw);
                    }

                    return false;
                }

                var folders = new List<string>();

                while (NativeMethods.FindNextFile(h_file, out NativeModels.Win32FindData find_data))
                {
                    string item = path + find_data.cFileName;

                    if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        if (find_data.cFileName == "..")
                        {
                            continue;
                        }

                        folders.Add(item);
                        Folders.Add(item);
                    }
                    else
                    {
                        Files.Add(item);
                    }
                }

                NativeMethods.FindClose(h_file);

                if (folders.Count == 0)
                {
                    return false;
                }

                m_stack.Add(new Node
                {
                    Paths = folders
                });
            }

            // Search deeper.
            while (m_stack[depth].Index < m_stack[depth].Paths.Count)
            {
                // Exit if min_step is reached.
                if (ItemFound >= item_count)
                {
                    return true;
                }

                if (InternalSearch(item_count, depth + 1))
                {
                    return true;
                }

                m_stack[depth].Index++;
            }

            m_stack.RemoveAt(m_stack.Count - 1);
            return false;
        }
    }

    // SubFiles
    public static List<string> SubFiles(string path, string name)
    {
        NativeModels.FindExInfoLevel findInfoLevel;
        NativeModels.FIndexSearchOps indexSearchOps = NativeModels.FIndexSearchOps.FindExSearchNameMatch;
        int additionalFlags;

        if (Environment.OSVersion.Version.Major >= 6)
        {
            findInfoLevel = NativeModels.FindExInfoLevel.FindExInfoBasic;
            additionalFlags = FIND_FIRST_EX_LARGE_FETCH;
        }
        else
        {
            findInfoLevel = NativeModels.FindExInfoLevel.FindExInfoStandard;
            additionalFlags = 0;
        }

        path += "\\";
        var results = new List<string>();

        nint h_file = NativeMethods.FindFirstFileExFromApp(path + name, findInfoLevel,
            out _, indexSearchOps, nint.Zero, additionalFlags);

        if (h_file.ToInt64() == -1)
        {
            return results;
        }

        while (NativeMethods.FindNextFile(h_file, out NativeModels.Win32FindData find_data))
        {
            if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) != FileAttributes.Directory)
            {
                string fullpath = path + find_data.cFileName;
                results.Add(fullpath);
            }
        }

        NativeMethods.FindClose(h_file);
        return results;
    }

    public static async Task<string> ReadFileFromPath(string path)
    {
        SafeFileHandle h = NativeMethods.CreateFileFromApp(path, GENERIC_READ, 0, nint.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, new SafeFileHandle(nint.Zero, true));

        if (h.IsInvalid)
        {
            throw new IOException();
        }

        using (var stream = new FileStream(h, FileAccess.Read))
        using (var reader = new StreamReader(stream))
        {
            string text = await reader.ReadToEndAsync();
            return text;
        }
    }
}
