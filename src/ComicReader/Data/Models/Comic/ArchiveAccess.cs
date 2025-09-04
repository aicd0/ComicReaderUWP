// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.Data.Models.Comic;

#nullable disable

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

    public static async Task<Stream> TryGetFileStream(string location)
    {
        string base_path = GetBasePath(location, false);
        string sub_path = GetSubPath(location, false);
        StorageFile base_file = await Storage.TryGetFile(base_path);

        if (base_file == null)
        {
            return null;
        }

        return await TryGetFileStream(base_file, sub_path);
    }

    public static async Task<Stream> TryGetFileStream(StorageFile base_file, string sub_path)
    {
        if (sub_path.Length == 0)
        {
            try
            {
                return await base_file.OpenStreamForReadAsync();
            }
            catch (Exception e)
            {
                Log("Failed to access '" + base_file.Path + FileSeperator + sub_path + "'. " + e.ToString());
                return null;
            }
        }

        var mem_stream = new MemoryStream();

        TaskException result = await TryAccessArchiveStream(base_file, sub_path, async (stream) =>
        {
            await stream.CopyToAsync(mem_stream);
            mem_stream.Position = 0;
            return TaskException.Success;
        });

        if (!result.Successful())
        {
            mem_stream.Dispose();
            return null;
        }

        return mem_stream;
    }

    public static async Task<TaskException> TryAccessArchiveStream(StorageFile base_file, string sub_path, Func<Stream, Task<TaskException>> func)
    {
        if (base_file == null)
        {
            return TaskException.InvalidParameters;
        }

        Stream stream;
        try
        {
            stream = await base_file.OpenStreamForReadAsync();
        }
        catch (Exception e)
        {
            Log("Failed to access '" + base_file.Path + FileSeperator + sub_path + "'. " + e.ToString());
            return TaskException.Failure;
        }

        TaskException result = await TryAccessArchiveStream(stream, base_file.FileType, sub_path, func);
        stream.Dispose();
        return result;
    }

    private static void Log(string message)
    {
        Logger.I("ArchiveAccess", message);
    }

    public static async Task<TaskException> TryGetSubFiles(StorageFile base_file, string sub_path, List<string> output)
    {
        return await TryAccessDeepestArchive(base_file, sub_path,
            async (stream, ctx) =>
            {
                return await Task.Run(() =>
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
        public string Entry;
        public string Extension;
    }

    private static async Task<TaskException> TryAccessDeepestArchive(StorageFile baseFile, string subPath,
        Func<Stream, ArchiveAccessContext, Task<TaskException>> func)
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

        return await TryAccessArchiveStream(baseFile, subBasePath,
            async (stream) => await func(stream, ctx));
    }

    public static async Task<TaskException> TryReadEntries(Stream stream, string extension, Func<IArchiveEntry, Task<TaskException>> callback)
    {
        if (stream == null || !stream.CanRead)
        {
            return TaskException.InvalidParameters;
        }

        // Reader options
        var opts = new SharpCompress.Readers.ReaderOptions();
        int default_code_page = AppModel.DefaultArchiveCodePage;

        if (default_code_page > 0)
        {
            try
            {
                var encoding = Encoding.GetEncoding(default_code_page,
                    Encoding.Default.GetEncoder().Fallback, Encoding.Default.GetDecoder().Fallback);
                opts.ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding
                {
                    CustomDecoder = (data, x, y) => encoding.GetString(data)
                };
            }
            catch (Exception e)
            {
                Log("Failed to set up decoder. " + e.ToString());
            }
        }

        // Iterate entries
        switch (extension.ToLower())
        {
            case ".7z":
            case ".cb7":
                SharpCompress.Archives.SevenZip.SevenZipArchive archive;
                try
                {
                    archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(stream, opts);
                }
                catch (Exception e)
                {
                    Log("Failed to open 7z archive. " + e.ToString());
                    return TaskException.FileCorrupted;
                }

                using (archive)
                {
                    foreach (SharpCompress.Archives.SevenZip.SevenZipArchiveEntry raw_entry in archive.Entries)
                    {
                        var entry = new SevenZipArchiveEntry(raw_entry);
                        TaskException result = await callback(entry);
                        if (result == TaskException.StopIteration)
                        {
                            break;
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
                SharpCompress.Readers.IReader reader;
                try
                {
                    reader = SharpCompress.Readers.ReaderFactory.Open(stream, opts);
                }
                catch (Exception e)
                {
                    Log("Failed to open archive. " + e.ToString());
                    return TaskException.FileCorrupted;
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
                        catch (EndOfStreamException e)
                        {
                            Logger.E(TAG, "Unable to read next archive entry: unexpected end of the stream.", e);
                            break;
                        }
                        catch (Exception e)
                        {
                            Logger.F(TAG, "ArchiveReaderMoveNext", e);
                            break;
                        }

                        if (!hasNext)
                        {
                            break;
                        }

                        var entry = new ReaderArchiveEntry(reader);
                        TaskException result = await callback(entry);
                        if (result == TaskException.StopIteration)
                        {
                            break;
                        }
                    }
                }

                break;

            default:
                return TaskException.UnknownEnum;
        }

        return TaskException.Success;
    }

    private static async Task<TaskException> TryAccessArchiveStream(Stream stream, string extension, string sub_path, Func<Stream, Task<TaskException>> func)
    {
        if (stream == null)
        {
            Logger.AssertNotReachHere("F1487557CF9CC3A7");
            return TaskException.InvalidParameters;
        }

        return await TryAccessArchiveStreamInternal(stream, extension.ToLower(), sub_path, func);
    }

    private static async Task<TaskException> TryAccessArchiveStreamInternal(Stream stream,
        string extension, string sub_path, Func<Stream, Task<TaskException>> callback)
    {
        if (sub_path.Length == 0)
        {
            return await callback(stream);
        }

        string mainEntryName = GetBasePath(sub_path, false).Replace('/', '\\');
        string subEntryName = GetSubPath(sub_path, false);
        string filename = StringUtils.ItemNameFromPath(mainEntryName);
        string subExtension = StringUtils.ExtensionFromFilename(filename);
        TaskException result = TaskException.Unknown;
        bool entryExist = false;

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
                entryExist = true;
                result = await TryAccessArchiveStreamInternal(subStream, subExtension, subEntryName, callback);
                return TaskException.StopIteration;
            } while (false);

            return TaskException.Success;
        });

        if (!entryExist)
        {
            result = TaskException.FileNotFound;
        }

        return result;
    }

    private static async Task<TaskException> TryGetFileEntries(Stream stream, string extension, string baseEntryName, List<string> output)
    {
        baseEntryName = baseEntryName.Replace('/', '\\');
        if (baseEntryName.Length > 0 && baseEntryName[^1] != '\\')
        {
            baseEntryName += '\\';
        }

        return await TryReadEntries(stream, extension, (entry) =>
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

            return Task.FromResult(TaskException.Success);
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

        public string FullName => _reader.Entry.Key;
        public bool IsDirectory => _reader.Entry.IsDirectory;

        public Stream Open()
        {
            return _reader.OpenEntryStream();
        }
    }

    private class SevenZipArchiveEntry(SharpCompress.Archives.SevenZip.SevenZipArchiveEntry entry) : IArchiveEntry
    {
        private readonly SharpCompress.Archives.SevenZip.SevenZipArchiveEntry _entry = entry;

        public string FullName => _entry.Key;
        public bool IsDirectory => _entry.IsDirectory;

        public Stream Open()
        {
            return _entry.OpenEntryStream();
        }
    }
}
