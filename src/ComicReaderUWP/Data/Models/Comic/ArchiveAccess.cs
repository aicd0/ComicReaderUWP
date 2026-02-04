// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;

namespace ComicReaderUWP.Data.Models.Comic;

public class ArchiveAccess
{
    private const string TAG = nameof(ArchiveAccess);
    public const string FileSeperator = "\\\\";

    public static bool IsArchivePath(string path)
    {
        return GetFileSeperatorIndex(path, false) > -1;
    }

    public static string GetBasePath(string location, bool reverse)
    {
        int i = GetFileSeperatorIndex(location, reverse);
        if (i <= -1)
        {
            return location;
        }

        return location[..i];
    }

    public static string GetSubPath(string location, bool reverse)
    {
        int i = GetFileSeperatorIndex(location, reverse);
        if (i <= -1)
        {
            return "";
        }

        return location[(i + FileSeperator.Length)..];
    }

    public static async Task<Stream?> TryGetFileStream(string location)
    {
        string base_path = GetBasePath(location, false);
        string sub_path = GetSubPath(location, false);
        Windows.Storage.StorageFile? baseFile = await Storage.TryGetFile(base_path);
        if (baseFile == null)
        {
            return null;
        }

        return await TryGetFileStream(baseFile, sub_path);
    }

    public static async Task<Stream?> TryGetFileStream(Windows.Storage.StorageFile baseFile, string subPath)
    {
        if (subPath.Length == 0)
        {
            try
            {
                return await baseFile.OpenStreamForReadAsync();
            }
            catch (Exception e)
            {
                Logger.F(TAG, e);
                return null;
            }
        }

        var memStream = new MemoryStream();
        bool successful = false;
        try
        {
            await TryAccessArchiveStream(baseFile, subPath, async (stream) =>
            {
                stream.CopyTo(memStream);
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

    public static async Task TryAccessArchiveStream(Windows.Storage.StorageFile baseFile, string subPath, Func<Stream, Task> func)
    {
        if (baseFile is null)
        {
            return;
        }

        Stream stream;
        try
        {
            stream = await baseFile.OpenStreamForReadAsync();
        }
        catch (Exception e)
        {
            Logger.F(TAG, e);
            return;
        }

        try
        {
            await TryAccessArchiveStreamInternal(stream, baseFile.FileType.ToLower(), subPath, func);
        }
        finally
        {
            stream.Dispose();
        }
    }

    public static async Task TryGetSubFiles(Windows.Storage.StorageFile baseFile, string subPath, List<string> output)
    {
        await TryAccessDeepestArchive(baseFile, subPath, async (stream, ctx) =>
        {
            await Task.Run(() =>
            {
                return TryGetFileEntries(stream, ctx.Extension, ctx.Entry, output);
            });
        });
    }

    private static int GetFileSeperatorIndex(string path, bool reverse)
    {
        int i;
        if (path.StartsWith("\\\\"))
        {
            // Network location
            if (reverse)
            {
                i = path.LastIndexOf(FileSeperator);
                if (i <= 1)
                {
                    i = -1;
                }
            }
            else
            {
                i = path.IndexOf(FileSeperator, 2);
            }
        }
        else
        {
            i = reverse ? path.LastIndexOf(FileSeperator) : path.IndexOf(FileSeperator);
        }
        return i;
    }

    private class ArchiveAccessContext
    {
        public required string Entry;
        public required string Extension;
    }

    private static async Task TryAccessDeepestArchive(Windows.Storage.StorageFile baseFile, string subPath,
        Func<Stream, ArchiveAccessContext, Task> func)
    {
        string subBasePath = GetBasePath(subPath, reverse: true);
        string entry = GetSubPath(subPath, reverse: true);
        string extension = StringUtils.ExtensionFromFilename(subBasePath);

        if (entry.Length == 0)
        {
            entry = subBasePath;
            subBasePath = "";
            extension = baseFile.FileType;
        }

        var ctx = new ArchiveAccessContext
        {
            Entry = entry,
            Extension = extension,
        };

        await TryAccessArchiveStream(baseFile, subBasePath,
            async (stream) => await func(stream, ctx));
    }

    public static async Task TryReadEntries(Stream? stream, string extension, Func<IArchiveEntry, Task<ICallbackResult>> callback)
    {
        if (stream is null || !stream.CanRead)
        {
            Logger.F(TAG, "Stream is null or not readable.");
            return;
        }

        // Reader options
        var opts = new SharpCompress.Readers.ReaderOptions();
        int defaultCodePage = AppSettingsModel.Instance.DefaultArchiveCodePage;
        if (defaultCodePage > 0)
        {
            try
            {
                EncoderFallback encoderFallback = Encoding.Default.GetEncoder().Fallback ?? EncoderFallback.ReplacementFallback;
                DecoderFallback decoderFallback = Encoding.Default.GetDecoder().Fallback ?? DecoderFallback.ReplacementFallback;
                var encoding = Encoding.GetEncoding(defaultCodePage, encoderFallback, decoderFallback);
                opts.ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding
                {
                    CustomDecoder = (data, x, y) => encoding.GetString(data)
                };
            }
            catch (Exception e)
            {
                Logger.F(TAG, "Failed to set up decoder.", e);
            }
        }

        // Iterate entries
        switch (extension.ToLower())
        {
            case ".7z":
            case ".cb7":
                {
                    SharpCompress.Archives.SevenZip.SevenZipArchive archive;
                    try
                    {
                        archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(stream, opts);
                    }
                    catch (Exception e)
                    {
                        if (e is SharpCompress.Common.CryptographicException) // Encrypted archive not supported for now
                        {
                            Logger.E(TAG, e);
                        }
                        else
                        {
                            Logger.F(TAG, "Failed to open 7zip archive", e);
                        }

                        return;
                    }

                    using (archive)
                    {
                        foreach (SharpCompress.Archives.SevenZip.SevenZipArchiveEntry rawEntry in archive.Entries)
                        {
                            var entry = new SevenZipArchiveEntry(rawEntry);
                            ICallbackResult result = await callback(entry);
                            if (result == ICallbackResult.StopIteration)
                            {
                                break;
                            }
                        }
                    }
                }
                break;
            case ".bz2":
            case ".cbr":
            case ".cbt":
            case ".cbz":
            case ".gz":
            case ".rar":
            case ".tar":
            case ".xz":
            case ".zip":
                {
                    SharpCompress.Readers.IReader reader;
                    try
                    {
                        reader = SharpCompress.Readers.ReaderFactory.Open(stream, opts);
                    }
                    catch (Exception e)
                    {
                        if (e is InvalidDataException)
                        {
                            Logger.E(TAG, e);
                        }
                        else
                        {
                            Logger.F(TAG, "Failed to open archive", e);
                        }

                        return;
                    }

                    using (reader)
                    {
                        while (true)
                        {
                            bool hasNext;
                            try
                            {
                                hasNext = reader.MoveToNextEntry();
                            }
                            catch (Exception e)
                            {
                                if (e is EndOfStreamException ||
                                    e is SharpCompress.Common.CryptographicException || // Encrypted archive not supported for now
                                    e is SharpCompress.Common.IncompleteArchiveException)
                                {
                                    Logger.E(TAG, e);
                                }
                                else
                                {
                                    Logger.F(TAG, "Failed to read next archive entry", e);
                                }

                                break;
                            }

                            if (!hasNext)
                            {
                                break;
                            }

                            var entry = new ReaderArchiveEntry(reader);
                            ICallbackResult result = await callback(entry);
                            if (result == ICallbackResult.StopIteration)
                            {
                                break;
                            }
                        }
                    }
                }
                break;
            default:
                Logger.F(TAG, "Unsupported archive format: " + extension);
                return;
        }
    }

    private static async Task TryAccessArchiveStreamInternal(Stream stream,
        string extension, string subPath, Func<Stream, Task> callback)
    {
        if (subPath.Length == 0)
        {
            await callback(stream);
            return;
        }

        string mainEntryName = GetBasePath(subPath, false).Replace('/', '\\');
        string subEntryName = GetSubPath(subPath, false);
        string filename = StringUtils.ItemNameFromPath(mainEntryName);
        string subExtension = StringUtils.ExtensionFromFilename(filename);
        await TryReadEntries(stream, extension, async (entry) =>
        {
            do
            {
                if (entry.IsDirectory)
                {
                    break;
                }

                string entryName = entry.FullName.Replace('/', '\\');
                if (!entryName.Equals(mainEntryName))
                {
                    break;
                }

                using Stream subStream = entry.Open();
                await TryAccessArchiveStreamInternal(subStream, subExtension, subEntryName, callback);
                return ICallbackResult.StopIteration;
            } while (false);

            return ICallbackResult.Continue;
        });
    }

    private static async Task TryGetFileEntries(Stream stream, string extension, string baseEntryName, List<string> output)
    {
        baseEntryName = baseEntryName.Replace('/', '\\');
        if (baseEntryName.Length > 0 && baseEntryName[^1] != '\\')
        {
            baseEntryName += '\\';
        }

        await TryReadEntries(stream, extension, (entry) =>
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

            return Task.FromResult(ICallbackResult.Continue);
        });
    }

    public interface IArchiveEntry
    {
        string FullName { get; }
        bool IsDirectory { get; }

        Stream Open();
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

    private class SevenZipArchiveEntry(SharpCompress.Archives.SevenZip.SevenZipArchiveEntry entry) : IArchiveEntry
    {
        private readonly SharpCompress.Archives.SevenZip.SevenZipArchiveEntry _entry = entry;

        public string FullName => _entry.Key ?? string.Empty;
        public bool IsDirectory => _entry.IsDirectory;

        public Stream Open()
        {
            return _entry.OpenEntryStream();
        }
    }

    public enum ICallbackResult
    {
        Continue,
        StopIteration,
    }
}
