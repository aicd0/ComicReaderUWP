// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
            return string.Empty;
        }

        return location[(i + FileSeperator.Length)..];
    }

    public static Stream? TryGetFileStream(string location)
    {
        string basePath = GetBasePath(location, false);
        string subPath = GetSubPath(location, false);
        return TryGetFileStream(basePath, subPath);
    }

    public static Stream? TryGetFileStream(string basePath, string subPath)
    {
        if (subPath.Length == 0)
        {
            return TryReadFile(basePath);
        }

        var memStream = new MemoryStream();
        bool successful = false;
        try
        {
            TryAccessArchiveStream(basePath, subPath, stream =>
            {
                try
                {
                    stream.CopyTo(memStream);
                }
                catch (IOException e)
                {
                    // Stream was too long.
                    Logger.F(TAG, e);
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

    public static void TryAccessArchiveStream(string basePath, string subPath, Action<Stream> callback)
    {
        using Stream? stream = TryReadFile(basePath);
        if (stream is null)
        {
            return;
        }

        string extension = Path.GetExtension(basePath).ToLower();
        TryAccessArchiveStreamInternal(stream, extension, subPath, callback);
    }

    public static void TryGetSubFiles(string basePath, string subPath, List<string> output)
    {
        TryAccessDeepestArchive(basePath, subPath, (stream, ctx) =>
        {
            TryGetFileEntries(stream, ctx.Extension, ctx.Entry, output);
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

    private static FileStream? TryReadFile(string path)
    {
        string ErrorMessage()
        {
            return $"Unable to read file: {path}";
        }

        try
        {
            return File.OpenRead(path);
        }
        catch (FileNotFoundException e)
        {
            Logger.E(TAG, ErrorMessage(), e);
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            Logger.E(TAG, ErrorMessage(), e);
            return null;
        }
        catch (IOException e)
        {
            Logger.E(TAG, ErrorMessage(), e);
            return null;
        }
        catch (Exception e)
        {
            Logger.F(TAG, ErrorMessage(), e);
            return null;
        }
    }

    private static void TryAccessDeepestArchive(string basePath, string subPath, Action<Stream, ArchiveAccessContext> callback)
    {
        string subBasePath = GetBasePath(subPath, reverse: true);
        string entry = GetSubPath(subPath, reverse: true);
        string extension = Path.GetExtension(subBasePath);

        if (entry.Length == 0)
        {
            entry = subBasePath;
            subBasePath = string.Empty;
            extension = Path.GetExtension(basePath);
        }

        var ctx = new ArchiveAccessContext
        {
            Entry = entry,
            Extension = extension,
        };

        TryAccessArchiveStream(basePath, subBasePath, stream => callback(stream, ctx));
    }

    public static void TryReadEntries(Stream stream, string extension, Func<IArchiveEntry, ICallbackResult> callback)
    {
        if (!stream.CanRead)
        {
            Logger.F(TAG, "Stream is not readable");
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
                Logger.F(TAG, "Failed to set up a decoder", e);
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
                    catch (SharpCompress.Common.CryptographicException e)
                    {
                        Logger.E(TAG, e);
                        return;
                    }
                    catch (Exception e)
                    {
                        Logger.F(TAG, e);
                        return;
                    }

                    using (archive)
                    {
                        foreach (SharpCompress.Archives.SevenZip.SevenZipArchiveEntry rawEntry in archive.Entries)
                        {
                            var entry = new SevenZipArchiveEntry(rawEntry);
                            ICallbackResult result = callback(entry);
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
                    catch (EndOfStreamException e)
                    {
                        Logger.E(TAG, e);
                        return;
                    }
                    catch (InvalidDataException e)
                    {
                        Logger.E(TAG, e);
                        return;
                    }
                    catch (Exception e)
                    {
                        Logger.F(TAG, e);
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
                            catch (SharpCompress.Compressors.Deflate.ZlibException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (SharpCompress.Common.CryptographicException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (SharpCompress.Common.IncompleteArchiveException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (SharpCompress.Common.InvalidFormatException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (SharpCompress.Common.MultiVolumeExtractionException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (EndOfStreamException e)
                            {
                                Logger.E(TAG, e);
                                break;
                            }
                            catch (Exception e)
                            {
                                Logger.F(TAG, e);
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
                }
                break;

            default:
                Logger.F(TAG, "Unsupported archive format: " + extension);
                return;
        }
    }

    private static void TryAccessArchiveStreamInternal(Stream stream, string extension, string subPath, Action<Stream> callback)
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
        TryReadEntries(stream, extension, entry =>
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
            catch (SharpCompress.Common.CryptographicException e)
            {
                Logger.E(TAG, e);
                return ICallbackResult.StopIteration;
            }
            catch (Exception e)
            {
                Logger.F(TAG, e);
                return ICallbackResult.StopIteration;
            }

            try
            {
                TryAccessArchiveStreamInternal(subStream, subExtension, subEntryName, callback);
            }
            finally
            {
                try
                {
                    subStream.Dispose();
                }
                catch (SharpCompress.Compressors.Deflate.ZlibException e)
                {
                    Logger.E(TAG, e);
                }
                catch (Exception e)
                {
                    Logger.F(TAG, e);
                }
            }

            return ICallbackResult.StopIteration;
        });
    }

    private static void TryGetFileEntries(Stream stream, string extension, string baseEntryName, List<string> output)
    {
        baseEntryName = baseEntryName.Replace('/', '\\');
        if (baseEntryName.Length > 0 && baseEntryName[^1] != '\\')
        {
            baseEntryName += '\\';
        }

        TryReadEntries(stream, extension, entry =>
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
    }

    private class ArchiveAccessContext
    {
        public required string Entry;
        public required string Extension;
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
