// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReader.Common.Legacy;
using ComicReader.Data.Models;
using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.Common.Utils;

#nullable disable

public abstract class ArchiveEntry
{
    public abstract string FullName { get; }
    public abstract bool IsDirectory { get; }

    public abstract Stream Open();
}

public class ReaderArchiveEntry : ArchiveEntry
{
    public ReaderArchiveEntry(SharpCompress.Readers.IReader reader)
    {
        _reader = reader;
    }

    private readonly SharpCompress.Readers.IReader _reader;

    public override string FullName => _reader.Entry.Key;
    public override bool IsDirectory => _reader.Entry.IsDirectory;

    public override Stream Open()
    {
        return _reader.OpenEntryStream();
    }
}

public class SevenZipArchiveEntry : ArchiveEntry
{
    public SevenZipArchiveEntry(SharpCompress.Archives.SevenZip.SevenZipArchiveEntry entry)
    {
        _entry = entry;
    }

    private readonly SharpCompress.Archives.SevenZip.SevenZipArchiveEntry _entry;

    public override string FullName => _entry.Key;
    public override bool IsDirectory => _entry.IsDirectory;

    public override Stream Open()
    {
        return _entry.OpenEntryStream();
    }
}

public class ArchiveAccess
{
    private const string TAG = "ArchiveAccess";
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

    private static async Task<TaskException> TryAccessDeepestArchive(StorageFile base_file, string sub_path,
        Func<Stream, ArchiveAccessContext, Task<TaskException>> func)
    {
        string sub_base_path = GetBasePath(sub_path, reverse: true);
        string entry = GetSubPath(sub_path, reverse: true);
        string extension = StringUtils.ExtensionFromFilename(sub_base_path);

        if (entry.Length == 0)
        {
            entry = sub_base_path;
            sub_base_path = "";
            extension = base_file.FileType;
        }

        var ctx = new ArchiveAccessContext
        {
            Entry = entry,
            Extension = extension,
        };

        return await TryAccessArchiveStream(base_file, sub_base_path,
            async (stream) => await func(stream, ctx));
    }

    public static async Task<TaskException> TryReadEntries(Stream stream, string extension, Func<ArchiveEntry, Task<TaskException>> callback)
    {
        if (stream == null || !stream.CanRead)
        {
            return TaskException.InvalidParameters;
        }

        // Reader options.
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

        // Iterate entries.
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

        string main_entry_name = GetBasePath(sub_path, false).Replace('/', '\\');
        string sub_entry_name = GetSubPath(sub_path, false);
        string filename = StringUtils.ItemNameFromPath(main_entry_name);
        string sub_extension = StringUtils.ExtensionFromFilename(filename);
        TaskException result = TaskException.Unknown;
        bool entry_exist = false;

        await TryReadEntries(stream, extension, async (entry) =>
        {
            do
            {
                if (entry.IsDirectory)
                {
                    break;
                }

                string entry_name = entry.FullName.Replace('/', '\\');

                if (!entry_name.Equals(main_entry_name))
                {
                    break;
                }

                using (Stream sub_stream = entry.Open())
                {
                    entry_exist = true;
                    result = await TryAccessArchiveStreamInternal(sub_stream, sub_extension, sub_entry_name, callback);
                    return TaskException.StopIteration;
                }
            } while (false);

            return TaskException.Success;
        });

        if (!entry_exist)
        {
            result = TaskException.FileNotFound;
        }

        return result;
    }

    private static async Task<TaskException> TryGetFileEntries(Stream stream, string extension, string base_entry_name, List<string> output)
    {
        base_entry_name = base_entry_name.Replace('/', '\\');
        if (base_entry_name.Length > 0 && base_entry_name[base_entry_name.Length - 1] != '\\')
        {
            base_entry_name += '\\';
        }

        return await TryReadEntries(stream, extension, (entry) =>
        {
            do
            {
                if (entry.IsDirectory)
                {
                    break;
                }

                string entry_name = entry.FullName.Replace('/', '\\');
                if (!StringUtils.IsBeginWith(entry_name, base_entry_name))
                {
                    break;
                }

                string subpath = entry_name.Substring(base_entry_name.Length);
                if (subpath.Length == 0)
                {
                    break;
                }

                output.Add(subpath);
            } while (false);

            return Task.FromResult(TaskException.Success);
        });
    }
}
