// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common.Legacy;

public enum PathType
{
    Folder,
    File,
}

public class SearchContext
{
    public List<string> Folders { get; private set; } = new List<string>();
    public List<string> Files { get; private set; } = new List<string>();
    public List<string> NoAccessItems { get; private set; } = new List<string>();
    public int ItemFound => Folders.Count + Files.Count;

    private class PathInfo
    {
        public PathType Type;
        public string Path;
        public StorageItemSearchContext Ctx = null;
        public List<PathInfo> SubItems = new();
    }

    private class Node
    {
        public List<PathInfo> Paths;
        public int Index = 0;

        public PathInfo CurrentPath => Paths[Index];
    }

    private bool m_initial_search = true;
    private readonly List<Node> m_stack = new();
    private readonly int m_max_depth;

    public SearchContext(string path, PathType type, int max_depth = -1)
    {
        var path_info = new PathInfo
        {
            Type = type,
            Path = path,
        };

        m_stack.Add(new Node
        {
            Paths = new List<PathInfo>
            {
                path_info
            }
        });

        m_max_depth = max_depth;
    }

    public async Task<bool> Search(int min_items)
    {
        return await Task.Run(delegate
        {
            Folders.Clear();
            Files.Clear();
            NoAccessItems.Clear();

            if (m_stack.Count == 0)
            {
                return false;
            }

            if (m_initial_search)
            {
                Logger.Assert(m_stack.Count == 1, "A928F82A1210EAEC");
                m_initial_search = false;

                foreach (PathInfo path_info in m_stack[0].Paths)
                {
                    Folders.Add(path_info.Path);
                }
            }

            bool not_end = InternalSearch(min_items);
            return ItemFound > 0 || not_end;
        });
    }

    private bool InternalSearch(int min_items, int depth = 0)
    {
        if (depth >= m_stack.Count)
        {
            // Visit current node.
            PathInfo path_info = m_stack[m_stack.Count - 1].CurrentPath;
            SetSearchContext(path_info);
            var folders = new List<string>();
            var files = new List<string>();
            var no_access_items = new List<string>();
            bool not_finish = true;

            while (min_items > ItemFound && not_finish)
            {
                folders.Clear();
                files.Clear();
                no_access_items.Clear();
                not_finish = path_info.Ctx.Search(folders, files, no_access_items, min_items - ItemFound);
                Folders.AddRange(folders);
                Files.AddRange(files);
                NoAccessItems.AddRange(no_access_items);

                foreach (string file in files)
                {
                    string filename = StringUtils.ItemNameFromPath(file);
                    string extension = StringUtils.ExtensionFromFilename(filename);

                    if (AppInfoProvider.IsSupportedArchiveExtension(extension))
                    {
                        path_info.SubItems.Add(new PathInfo
                        {
                            Path = file,
                            Type = PathType.File,
                        });
                    }
                }
            }

            if (not_finish)
            {
                return true;
            }

            if (path_info.SubItems.Count == 0)
            {
                return false;
            }

            m_stack.Add(new Node
            {
                Paths = path_info.SubItems,
            });
        }

        if (m_max_depth < 0 || depth < m_max_depth)
        {
            // Search deeper.
            while (m_stack[depth].Index < m_stack[depth].Paths.Count)
            {
                // Exit if min_step is reached.
                if (ItemFound >= min_items)
                {
                    return true;
                }

                if (InternalSearch(min_items, depth + 1))
                {
                    return true;
                }

                m_stack[depth].Index++;
            }
        }

        m_stack.RemoveAt(m_stack.Count - 1);
        return false;
    }

    private void SetSearchContext(PathInfo path_info)
    {
        if (path_info.Ctx != null)
        {
            return;
        }

        switch (path_info.Type)
        {
            case PathType.Folder:
                path_info.Ctx = new FolderSearchContext(path_info.Path);
                break;
            case PathType.File:
                path_info.Ctx = new ArchiveSearchContext(path_info.Path);
                break;
            default:
                break;
        }
    }
}

public abstract class StorageItemSearchContext
{
    public abstract bool Search(List<string> folders, List<string> files, List<string> no_access_items, int min_items);
}

public class FolderSearchContext : StorageItemSearchContext
{
    readonly Win32IO.SubItemDeepContext m_ctx;

    public FolderSearchContext(string path)
    {
        m_ctx = new Win32IO.SubItemDeepContext(path);
    }

    public override bool Search(List<string> folders, List<string> files, List<string> no_access_items, int min_items)
    {
        bool not_finish = m_ctx.Search((uint)min_items);
        folders.AddRange(m_ctx.Folders);
        files.AddRange(m_ctx.Files);
        no_access_items.AddRange(m_ctx.NoAccessFolders);
        return not_finish;
    }
}

public class ArchiveSearchContext : StorageItemSearchContext
{
    private readonly string m_path;
    private readonly string m_extension;

    public ArchiveSearchContext(string path)
    {
        m_path = path;
        m_extension = StringUtils.ExtensionFromFilename(path);
    }

    public override bool Search(List<string> folders, List<string> files, List<string> no_access_items, int min_items)
    {
        using Stream stream = ArchiveAccess.TryGetFileStream(m_path).Result;
        var sub_folders = new HashSet<string>();

        ArchiveAccess.TryReadEntries(stream, m_extension, (entry) =>
        {
            string path = entry.FullName.Replace('/', '\\');
            if (entry.IsDirectory)
            {
                sub_folders.Add(path[..^1]);
            }
            else
            {
                files.Add(m_path + ArchiveAccess.FileSeperator + path);

                for (int i = 0; (i = path.IndexOf('\\', i)) >= 0; ++i)
                {
                    sub_folders.Add(path.Substring(0, i));
                }
            }

            return Task.FromResult(TaskException.Success);
        }).Wait();

        foreach (string sub_folder in sub_folders)
        {
            folders.Add(m_path + ArchiveAccess.FileSeperator + sub_folder);
        }

        return false;
    }
}
