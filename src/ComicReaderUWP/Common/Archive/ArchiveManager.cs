// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Common.Archive;

internal static class ArchiveManager
{
    public const string ARCHIVE_SEP = "\\\\";

    private const string TAG = nameof(ArchiveManager);

    public static bool IsArchivePath(string path)
    {
        return FindArchiveSeperator(path, false) > -1;
    }

    public static (string, string) SplitPath(string path, bool preferNested = false)
    {
        int i = FindArchiveSeperator(path, preferNested);
        if (i <= -1)
        {
            return (path, string.Empty);
        }

        return (path[..i], path[(i + ARCHIVE_SEP.Length)..]);
    }

    public static Stream OpenEntry(string basePath, string subPath)
    {
        if (subPath.Length == 0)
        {
            return OpenFile(basePath) ?? throw new ArchiveIOException();
        }

        var memStream = new MemoryStream();
        bool successful = false;
        try
        {
            OpenEntry(basePath, subPath, context =>
            {
                try
                {
                    context.Stream.CopyTo(memStream);
                }
                catch (SharpCompress.Compressors.Deflate.ZlibException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    return;
                }

                memStream.Position = 0;
                successful = true;
            }, allowCacheCreate: true);
        }
        finally
        {
            if (!successful)
            {
                memStream.Dispose();
                memStream = null;
            }
        }

        if (memStream is null)
        {
            throw new ArchiveIOException();
        }

        return memStream;
    }

    public static IEnumerable<string> ListFileEntries(string basePath, string subPath)
    {
        (string subBasePath, string subSubPath) = SplitPath(subPath, preferNested: true);
        string extension = Path.GetExtension(subBasePath);

        if (subSubPath.Length == 0)
        {
            subSubPath = subBasePath;
            subBasePath = string.Empty;
            extension = Path.GetExtension(basePath);
        }

        subSubPath = subSubPath.Replace('/', '\\');
        if (subSubPath.Length > 0 && subSubPath[^1] != '\\')
        {
            subSubPath += '\\';
        }

        List<string> result = [];

        OpenEntry(basePath, subBasePath, context =>
        {
            VisitEntriesInternal(context, entry =>
            {
                do
                {
                    if (entry.IsDirectory)
                    {
                        break;
                    }

                    string entryName = entry.FullName.Replace('/', '\\');
                    if (!StringUtils.IsBeginWith(entryName, subSubPath))
                    {
                        break;
                    }

                    string subpath = entryName[subSubPath.Length..];
                    if (subpath.Length == 0)
                    {
                        break;
                    }

                    result.Add(subpath);
                } while (false);

                return ICallbackResult.Continue;
            });
        });

        return result;
    }

    public static void VisitEntries(string path, Func<IArchiveEntry, ICallbackResult> callback)
    {
        (string basePath, string subPath) = SplitPath(path);

        OpenEntry(basePath, subPath, context =>
        {
            VisitEntriesInternal(context, callback);
        });
    }

    private static void OpenEntry(string basePath, string subPath, Action<EntryContext> callback, bool allowCacheCreate = false)
    {
        using Stream? stream = OpenFile(basePath) ?? throw new ArchiveIOException();

        EntryContext context = new()
        {
            Stream = stream,
            Extension = Path.GetExtension(basePath).ToLower(),
            SubPath = string.Empty,
            BasePath = basePath,
            AllowCacheCreate = allowCacheCreate,
        };
        OpenEntry(context, subPath, callback);
    }

    private static void OpenEntry(EntryContext context, string subPath, Action<EntryContext> callback)
    {
        if (subPath.Length == 0)
        {
            callback(context);
            return;
        }

        (string baseEntryName, string subEntryName) = SplitPath(subPath);
        string filename = StringUtils.ItemNameFromPath(baseEntryName);
        string subExtension = StringUtils.ExtensionFromFilename(filename);

        VisitEntriesInternal(context, entry =>
        {
            if (entry.IsDirectory)
            {
                return ICallbackResult.Continue;
            }

            string entryName = entry.FullName.Replace('/', '\\');
            if (!entryName.Equals(baseEntryName))
            {
                return ICallbackResult.Continue;
            }

            Stream subStream;
            try
            {
                subStream = entry.Open();
            }
            catch (SharpCompress.Common.CryptographicException ex)
            {
                Logger.E(TAG, ex);
                return ICallbackResult.StopIteration;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
                return ICallbackResult.StopIteration;
            }

            try
            {
                EntryContext subContext = new()
                {
                    Stream = subStream,
                    Extension = subExtension,
                    SubPath = CombinePaths(context.SubPath, baseEntryName),
                    BasePath = context.BasePath,
                    AllowCacheCreate = context.AllowCacheCreate,
                };
                OpenEntry(subContext, subEntryName, callback);
            }
            finally
            {
                try
                {
                    subStream.Dispose();
                }
                catch (SharpCompress.Compressors.Deflate.ZlibException ex)
                {
                    Logger.E(TAG, ex);
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                }
            }

            return ICallbackResult.StopIteration;
        });
    }

    private static void VisitEntriesInternal(EntryContext context, Func<IArchiveEntry, ICallbackResult> callback)
    {
        if (!context.Stream.CanRead)
        {
            Logger.F(TAG, "Stream is not readable");
            return;
        }

        if (!context.IsCache)
        {
            using Stream? cachedStream = ArchiveCacheManager.Get(context.BasePath, context.SubPath);
            if (cachedStream is not null)
            {
                VisitEntriesFromCache(cachedStream, context, callback);
                return;
            }
        }

        SharpCompress.Readers.ReaderOptions opts = context.IsCache
            ? ArchiveCacheManager.CreateCachedArchiveReaderOptions()
            : CreateReaderOptions(context.Extension);

        Stream? tempStream = null;
        SharpCompress.Archives.IArchive? archive = null;
        SharpCompress.Readers.IReader? reader = null;
        try
        {
            Stream? CreateSeekableStream()
            {
                MemoryStream seekable = new();
                try
                {
                    context.Stream.CopyTo(seekable);
                    seekable.Position = 0;
                    tempStream = seekable;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    seekable.Dispose();
                }

                return tempStream;
            }

            Stream? seekableStream = context.Stream.CanSeek ? context.Stream : null;

            // Open archive if possible
            switch (context.Extension.ToLower())
            {
                case ".7z":
                case ".cb7":
                    {
                        seekableStream ??= CreateSeekableStream();
                        if (seekableStream is null)
                        {
                            return;
                        }

                        try
                        {
                            archive = SharpCompress.Archives.SevenZip.SevenZipArchive.OpenArchive(seekableStream, opts);
                        }
                        catch (SharpCompress.Common.CryptographicException ex)
                        {
                            Logger.E(TAG, ex);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.F(TAG, ex);
                            break;
                        }
                    }

                    break;
            }

            if (archive is null && seekableStream is not null)
            {
                try
                {
                    seekableStream.Position = 0;
                    archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(seekableStream, opts);
                }
                catch (SharpCompress.Common.ArchiveOperationException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    return;
                }
            }

            // Open reader
            if (archive is not null)
            {
                try
                {
                    if (archive.IsSolid)
                    {
                        if (context.AllowCacheCreate)
                        {
                            using Stream? cachedStream = ArchiveCacheManager.GetOrCreate(context.BasePath, context.SubPath, archive);
                            if (cachedStream is not null)
                            {
                                VisitEntriesFromCache(cachedStream, context, callback);
                                return;
                            }
                        }

                        reader = archive.ExtractAllEntries();
                    }
                }
                catch (SharpCompress.Common.CryptographicException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    return;
                }
            }

            if (reader is null)
            {
                try
                {
                    reader = SharpCompress.Readers.ReaderFactory.OpenReader(context.Stream, opts);
                }
                catch (SharpCompress.Common.InvalidFormatException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (EndOfStreamException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (InvalidDataException ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    return;
                }
            }

            // Enumerate entries
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = reader.MoveToNextEntry();
                }
                catch (SharpCompress.Compressors.Deflate.ZlibException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (SharpCompress.Common.CryptographicException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (SharpCompress.Common.IncompleteArchiveException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (SharpCompress.Common.InvalidFormatException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (SharpCompress.Common.MultiVolumeExtractionException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (EndOfStreamException ex)
                {
                    Logger.E(TAG, ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                var entry = new ReaderArchiveEntry(reader);
                ICallbackResult result = callback(entry);
                if (result == ICallbackResult.StopIteration)
                {
                    break;
                }
            }
        }
        finally
        {
            reader?.Dispose();
            archive?.Dispose();
            tempStream?.Dispose();
        }
    }

    private static void VisitEntriesFromCache(Stream cachedStream, EntryContext context, Func<IArchiveEntry, ICallbackResult> callback)
    {
        EntryContext cachedContext = new()
        {
            Stream = cachedStream,
            Extension = ".zip",
            SubPath = context.SubPath,
            BasePath = context.BasePath,
            IsCache = true,
            AllowCacheCreate = context.AllowCacheCreate,
        };
        VisitEntriesInternal(cachedContext, callback);
    }

    private static SharpCompress.Readers.ReaderOptions CreateReaderOptions(string extensionHint)
    {
        SharpCompress.Common.IArchiveEncoding? archiveEncoding = null;
        int defaultCodePage = AppSettingsModel.Instance.DefaultArchiveCodePage;
        if (defaultCodePage > 0)
        {
            try
            {
                EncoderFallback encoderFallback = Encoding.Default.GetEncoder().Fallback ?? EncoderFallback.ReplacementFallback;
                DecoderFallback decoderFallback = Encoding.Default.GetDecoder().Fallback ?? DecoderFallback.ReplacementFallback;
                var encoding = Encoding.GetEncoding(defaultCodePage, encoderFallback, decoderFallback);
                archiveEncoding = new SharpCompress.Common.ArchiveEncoding()
                {
                    CustomDecoder = (data, x, y, type) => encoding.GetString(data)
                };
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "Failed to set up a decoder", ex);
            }
        }

        return new()
        {
            ArchiveEncoding = archiveEncoding ?? new SharpCompress.Common.ArchiveEncoding(),
            ExtensionHint = extensionHint,
        };
    }

    private static FileStream? OpenFile(string path)
    {
        string ErrorMessage()
        {
            return $"Unable to read file: {path}";
        }

        try
        {
            return File.OpenRead(path);
        }
        catch (FileNotFoundException ex)
        {
            Logger.E(TAG, ErrorMessage(), ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.E(TAG, ErrorMessage(), ex);
            return null;
        }
        catch (IOException ex)
        {
            Logger.E(TAG, ErrorMessage(), ex);
            return null;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ErrorMessage(), ex);
            return null;
        }
    }

    private static string CombinePaths(string basePath, string subPath)
    {
        if (basePath.Length == 0)
        {
            return subPath;
        }

        if (subPath.Length == 0)
        {
            return basePath;
        }

        return basePath + ARCHIVE_SEP + subPath;
    }

    private static int FindArchiveSeperator(string path, bool preferNested)
    {
        int i;
        if (path.StartsWith("\\\\"))
        {
            // Network location
            if (preferNested)
            {
                i = path.LastIndexOf(ARCHIVE_SEP);
                if (i <= 1)
                {
                    i = -1;
                }
            }
            else
            {
                i = path.IndexOf(ARCHIVE_SEP, 2);
            }
        }
        else
        {
            i = preferNested ? path.LastIndexOf(ARCHIVE_SEP) : path.IndexOf(ARCHIVE_SEP);
        }

        return i;
    }

    public interface IArchiveEntry
    {
        string FullName { get; }
        bool IsDirectory { get; }

        Stream Open();
    }

    public enum ICallbackResult
    {
        Continue,
        StopIteration,
    }

    private struct EntryContext
    {
        public required Stream Stream;
        public required string Extension;
        public required string SubPath;
        public required string BasePath;

        public bool IsCache = false;
        public bool AllowCacheCreate = false;

        public EntryContext() { }
    }

    private class ReaderArchiveEntry(SharpCompress.Readers.IReader reader) : IArchiveEntry
    {
        private readonly SharpCompress.Readers.IReader _reader = reader;

        public string FullName => _reader.Entry.Key ?? string.Empty;
        public bool IsDirectory => _reader.Entry.IsDirectory;

        public Stream Open()
        {
            return _reader.OpenEntryStream();
        }
    }

    private class SevenZipArchiveEntry(SharpCompress.Archives.IArchiveEntry entry) : IArchiveEntry
    {
        private readonly SharpCompress.Archives.IArchiveEntry _entry = entry;

        public string FullName => _entry.Key ?? string.Empty;
        public bool IsDirectory => _entry.IsDirectory;

        public Stream Open()
        {
            return _entry.OpenEntryStream();
        }
    }
}
