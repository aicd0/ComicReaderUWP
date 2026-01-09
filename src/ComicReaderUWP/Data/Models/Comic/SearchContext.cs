// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.DebugTools;

namespace ComicReaderUWP.Data.Models.Comic;

public enum PathType
{
    Folder,
    File,
}

public class SearchContext
{
    private const string TAG = nameof(SearchContext);

    public List<string> Folders { get; private set; } = [];
    public List<string> Files { get; private set; } = [];
    public List<string> NoAccessItems { get; private set; } = [];
    public int ItemFound => Folders.Count + Files.Count;

    private bool _initialSearch = true;
    private readonly List<Node> _stack = [];
    private readonly int _maxDepth;

    public SearchContext(string path, PathType type, int maxDepth = -1)
    {
        var pathInfo = new PathInfo(type, path);
        _stack.Add(new Node
        {
            Paths = [pathInfo]
        });

        _maxDepth = maxDepth;
    }

    public Task<bool> Search(int minItems)
    {
        return Task.Run(delegate
        {
            Folders.Clear();
            Files.Clear();
            NoAccessItems.Clear();

            if (_stack.Count == 0)
            {
                return false;
            }

            if (_initialSearch)
            {
                Logger.Assert(_stack.Count == 1, "A928F82A1210EAEC");
                _initialSearch = false;

                foreach (PathInfo pathInfo in _stack[0].Paths)
                {
                    Folders.Add(pathInfo.Path);
                }
            }

            bool notEnd = InternalSearch(minItems);
            return ItemFound > 0 || notEnd;
        });
    }

    private bool InternalSearch(int minItems, int depth = 0)
    {
        if (depth >= _stack.Count)
        {
            // Visit current node
            PathInfo pathInfo = _stack[^1].CurrentPath;
            var folders = new List<string>();
            var files = new List<string>();
            var noAccessItems = new List<string>();
            bool notFinish = true;

            while (minItems > ItemFound && notFinish)
            {
                folders.Clear();
                files.Clear();
                noAccessItems.Clear();

                try
                {
                    notFinish = pathInfo.Ctx.Search(folders, files, noAccessItems, minItems - ItemFound);
                }
                catch (Exception e)
                {
                    Logger.F(TAG, e);
                    notFinish = false;
                }

                Folders.AddRange(folders);
                Files.AddRange(files);
                NoAccessItems.AddRange(noAccessItems);

                foreach (string file in files)
                {
                    string filename = StringUtils.ItemNameFromPath(file);
                    string extension = StringUtils.ExtensionFromFilename(filename);
                    if (AppInfoProvider.IsSupportedArchiveExtension(extension))
                    {
                        pathInfo.SubItems.Add(new PathInfo(PathType.File, file));
                    }
                }
            }

            if (notFinish)
            {
                return true;
            }

            if (pathInfo.SubItems.Count == 0)
            {
                return false;
            }

            _stack.Add(new Node
            {
                Paths = pathInfo.SubItems,
            });
        }

        if (_maxDepth < 0 || depth < _maxDepth)
        {
            // Search deeper
            while (_stack[depth].Index < _stack[depth].Paths.Count)
            {
                // Exit if minStep is reached
                if (ItemFound >= minItems)
                {
                    return true;
                }

                if (InternalSearch(minItems, depth + 1))
                {
                    return true;
                }

                _stack[depth].Index++;
            }
        }

        _stack.RemoveAt(_stack.Count - 1);
        return false;
    }

    private class PathInfo
    {
        public readonly PathType Type;
        public readonly string Path;
        public readonly IStorageItemSearchContext Ctx;
        public readonly List<PathInfo> SubItems = [];

        public PathInfo(PathType pathType, string path)
        {
            Type = pathType;
            Path = path;

            Ctx = pathType switch
            {
                PathType.Folder => new FolderSearchContext(path),
                PathType.File => new ArchiveSearchContext(path),
                _ => throw new ArgumentException(null, nameof(pathType)),
            };
        }
    }

    private class Node
    {
        public required List<PathInfo> Paths;
        public int Index = 0;

        public PathInfo CurrentPath => Paths[Index];
    }

    private interface IStorageItemSearchContext
    {
        bool Search(List<string> folders, List<string> files, List<string> noAccessItems, int minItems);
    }

    private class FolderSearchContext(string path) : IStorageItemSearchContext
    {
        readonly Win32IO.SubItemDeepContext _ctx = new(path);

        public bool Search(List<string> folders, List<string> files, List<string> noAccessItems, int minItems)
        {
            bool notFinish = _ctx.Search((uint)minItems);
            folders.AddRange(_ctx.Folders);
            files.AddRange(_ctx.Files);
            noAccessItems.AddRange(_ctx.NoAccessFolders);
            return notFinish;
        }
    }

    private class ArchiveSearchContext(string path) : IStorageItemSearchContext
    {
        private readonly string _path = path;
        private readonly string _extension = StringUtils.ExtensionFromFilename(path);

        public bool Search(List<string> folders, List<string> files, List<string> noAccessItems, int minItems)
        {
            using Stream? stream = ArchiveAccess.TryGetFileStream(_path).Result;
            var subFolders = new HashSet<string>();
            ArchiveAccess.TryReadEntries(stream, _extension, (entry) =>
            {
                string path = entry.FullName.Replace('/', '\\');
                if (entry.IsDirectory)
                {
                    subFolders.Add(path[..^1]);
                }
                else
                {
                    files.Add(_path + ArchiveAccess.FileSeperator + path);
                    for (int i = 0; (i = path.IndexOf('\\', i)) >= 0; ++i)
                    {
                        subFolders.Add(path[..i]);
                    }
                }

                return Task.FromResult(ArchiveAccess.ICallbackResult.Continue);
            }).Wait();

            foreach (string subFolder in subFolders)
            {
                folders.Add(_path + ArchiveAccess.FileSeperator + subFolder);
            }

            return false;
        }
    }
}
