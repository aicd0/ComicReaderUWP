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

    public static string GetBasePath(string path, bool preferNested)
    {
        int i = FindArchiveSeperator(path, preferNested);
        if (i <= -1)
        {
            return path;
        }

        return path[..i];
    }

    public static string GetSubPath(string path, bool preferNested)
    {
        int i = FindArchiveSeperator(path, preferNested);
        if (i <= -1)
        {
            return string.Empty;
        }

        return path[(i + ARCHIVE_SEP.Length)..];
    }

    public static Stream? OpenEntry(string path)
    {
        string basePath = GetBasePath(path, false);
        string subPath = GetSubPath(path, false);
        return OpenEntry(basePath, subPath);
    }

    public static Stream? OpenEntry(string basePath, string subPath)
    {
        if (subPath.Length == 0)
        {
            return OpenFile(basePath);
        }

        var memStream = new MemoryStream();
        bool successful = false;
        try
        {
            OpenArchive(basePath, subPath, stream =>
            {
                try
                {
                    stream.CopyTo(memStream);
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
            });
        }
        finally
        {
            if (!successful)
            {
                memStream.Dispose();
                memStream = null;
            }
        }

        return memStream;
    }

    public static IEnumerable<string> ListFileEntries(string basePath, string subPath)
    {
        string subBasePath = GetBasePath(subPath, preferNested: true);
        string entry = GetSubPath(subPath, preferNested: true);
        string extension = Path.GetExtension(subBasePath);

        if (entry.Length == 0)
        {
            entry = subBasePath;
            subBasePath = string.Empty;
            extension = Path.GetExtension(basePath);
        }

        IEnumerable<string>? output = null;

        OpenArchive(basePath, subBasePath, stream =>
        {
            output = ListFileEntries(stream, extension, entry);
        });

        return output ?? [];
    }

    public static void VisitEntries(Stream stream, string extension, Func<IArchiveEntry, ICallbackResult> callback)
    {
        VisitEntriesInternal(stream, extension, callback);
    }

    private static void OpenArchive(string basePath, string subPath, Action<Stream> callback)
    {
        using Stream? stream = OpenFile(basePath);
        if (stream is null)
        {
            return;
        }

        string extension = Path.GetExtension(basePath).ToLower();
        OpenArchive(stream, extension, subPath, callback);
    }

    private static void OpenArchive(Stream stream, string extension, string subPath, Action<Stream> callback)
    {
        if (subPath.Length == 0)
        {
            callback(stream);
            return;
        }

        string mainEntryName = GetBasePath(subPath, false).Replace('/', '\\');
        string subEntryName = GetSubPath(subPath, false);
        string filename = StringUtils.ItemNameFromPath(mainEntryName);
        string subExtension = StringUtils.ExtensionFromFilename(filename);
        VisitEntriesInternal(stream, extension, entry =>
        {
            if (entry.IsDirectory)
            {
                return ICallbackResult.Continue;
            }

            string entryName = entry.FullName.Replace('/', '\\');
            if (!entryName.Equals(mainEntryName))
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
                OpenArchive(subStream, subExtension, subEntryName, callback);
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

    private static List<string> ListFileEntries(Stream stream, string extension, string baseEntryName)
    {
        List<string> output = [];

        baseEntryName = baseEntryName.Replace('/', '\\');
        if (baseEntryName.Length > 0 && baseEntryName[^1] != '\\')
        {
            baseEntryName += '\\';
        }

        VisitEntriesInternal(stream, extension, entry =>
        {
            do
            {
                if (entry.IsDirectory)
                {
                    break;
                }

                string entryName = entry.FullName.Replace('/', '\\');
                if (!StringUtils.IsBeginWith(entryName, baseEntryName))
                {
                    break;
                }

                string subpath = entryName[baseEntryName.Length..];
                if (subpath.Length == 0)
                {
                    break;
                }

                output.Add(subpath);
            } while (false);

            return ICallbackResult.Continue;
        });

        return output;
    }

    private static void VisitEntriesInternal(Stream stream, string extension, Func<IArchiveEntry, ICallbackResult> callback)
    {
        if (!stream.CanRead)
        {
            Logger.F(TAG, "Stream is not readable");
            return;
        }

        SharpCompress.Readers.ReaderOptions opts = CreateReaderOptions(extension);

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
                    stream.CopyTo(seekable);
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

            Stream? seekableStream = stream.CanSeek ? stream : null;

            // Open archive if possible
            switch (extension.ToLower())
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
                    reader = SharpCompress.Readers.ReaderFactory.OpenReader(stream, opts);
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
