// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;

namespace ComicReaderUWP.Common.Imaging;

internal class ImageLoaderSchedulerGroup
{
    private const string DEFAULT_GROUP_NAME = "Default";

    private static readonly ConcurrentDictionary<string, ImageLoaderSchedulerGroup> sGroups = new();

    private static readonly Lazy<ImageLoaderSchedulerGroup> sDefaultGroup = new(() => FromName(DEFAULT_GROUP_NAME));
    public static ImageLoaderSchedulerGroup Default => sDefaultGroup.Value;

    public string Name { get; }
    public int MaxConcurrentTasks { get; }

    private ImageLoaderSchedulerGroup(string name, int maxConcurrentTasks)
    {
        Name = name;
        MaxConcurrentTasks = maxConcurrentTasks;
    }

    public static ImageLoaderSchedulerGroup FromPath(string path)
    {
        string name = GetGroupName(path);
        return FromName(name);
    }

    private static ImageLoaderSchedulerGroup FromName(string name)
    {
        return sGroups.GetOrAdd(name, _ => new ImageLoaderSchedulerGroup(name, 1));
    }

    private static string GetGroupName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return DEFAULT_GROUP_NAME;
        }

        // Network location (UNC path), e.g. "\\NAS-1234\someLocation" or "//NAS-1234/someLocation".
        // The device name is the first segment after the leading slashes.
        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            string rest = path.Substring(2);
            int separatorIndex = rest.IndexOfAny(['\\', '/']);
            string deviceName = (separatorIndex < 0 ? rest : rest.Substring(0, separatorIndex)).Trim();
            return deviceName.Length > 0 ? "Network_" + deviceName : DEFAULT_GROUP_NAME;
        }

        // Drive letter path, e.g. "C:\file.txt", "C:\someFolder" or "C:".
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return "Drive_" + char.ToUpperInvariant(path[0]);
        }

        return DEFAULT_GROUP_NAME;
    }
}
