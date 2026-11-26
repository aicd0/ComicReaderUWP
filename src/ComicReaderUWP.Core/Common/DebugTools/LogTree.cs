// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Core.Common.DebugTools;

internal static class LogTree
{
    private const int MAX_NAME_LENGTH = 48;

    private const string EMPTY_TAG = "Empty";

    private static readonly char[] sInvalidFileNameChars = Path.GetInvalidFileNameChars();

    internal static void Append(string rootPath, IReadOnlyList<LogEntry> logs)
    {
        Dictionary<string, List<LogEntry>> batches = [];
        foreach (LogEntry item in logs)
        {
            if (!DebugModel.IsLogTreeWhitelisted(item.Tag))
            {
                continue;
            }

            foreach (string folderPath in GetFolderPaths(item.Tag))
            {
                if (!batches.TryGetValue(folderPath, out List<LogEntry>? batch))
                {
                    batch = [];
                    batches[folderPath] = batch;
                }

                batch.Add(item);
            }
        }

        string fileName = "log_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

        foreach (KeyValuePair<string, List<LogEntry>> pair in batches)
        {
            StringBuilder sb = new();
            foreach (LogEntry item in pair.Value)
            {
                sb.Append(item.DisplayMessage);
                sb.Append('\n');
            }
            string content = sb.ToString();

            try
            {
                string? folderPath = GetFolderPath(rootPath, pair.Key);
                if (folderPath is null)
                {
                    continue;
                }

                string filePath = Path.Combine(folderPath, fileName);
                Directory.CreateDirectory(folderPath);
                using StreamWriter writer = new(filePath, true, Encoding.UTF8);
                writer.Write(content);
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    internal static string[] GetFilePaths(string rootPath, string tag)
    {
        string? folderPath = GetFolderPath(rootPath, GetRelativePath(tag));
        if (folderPath is null || !Directory.Exists(folderPath))
        {
            return [];
        }

        string[] filePaths = Directory.GetFiles(folderPath, "log_*.txt");
        Array.Sort(filePaths, StringComparer.Ordinal);
        return filePaths;
    }

    internal static string GetRelativePath(string tag)
    {
        return string.Join('\\', GetLevelNames(tag));
    }

    internal static string ToName(string category)
    {
        StringBuilder name = new(category.Length);
        foreach (char c in category)
        {
            name.Append(Array.IndexOf(sInvalidFileNameChars, c) >= 0 ? '_' : c);
        }

        if (name.Length > MAX_NAME_LENGTH)
        {
            name.Length = MAX_NAME_LENGTH;
        }

        // Windows drops trailing dots and spaces, so "A." and "A" would be one folder — and "." or ".." would be a
        // folder this tree does not contain.
        string trimmed = name.ToString().TrimEnd('.', ' ');
        if (trimmed.Length == 0 || IsReservedName(trimmed))
        {
            return string.Empty;
        }

        return trimmed;
    }

    private static IEnumerable<string> GetLevelNames(string tag)
    {
        int found = 0;
        foreach (string category in tag.Split('/'))
        {
            string name = ToName(category);
            if (name.Length == 0)
            {
                continue;
            }

            found++;
            yield return name;
        }

        if (found == 0)
        {
            yield return EMPTY_TAG;
        }
    }

    private static IEnumerable<string> GetFolderPaths(string tag)
    {
        string path = string.Empty;
        foreach (string name in GetLevelNames(tag))
        {
            path = path.Length == 0 ? name : $"{path}\\{name}";
            yield return path;
        }
    }

    private static string? GetFolderPath(string rootPath, string relativePath)
    {
        string root = Path.GetFullPath(rootPath);
        string folderPath = Path.GetFullPath(Path.Combine(root, relativePath));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return folderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? folderPath : null;
    }

    private static bool IsReservedName(string name)
    {
        if (name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Length != 4)
        {
            return false;
        }

        bool devicePrefix = name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase);
        return devicePrefix && name[3] is >= '1' and <= '9';
    }
}
