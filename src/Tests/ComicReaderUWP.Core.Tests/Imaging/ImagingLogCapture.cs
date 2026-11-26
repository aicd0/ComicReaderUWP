// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Core.Tests.Imaging;

/// <summary>
/// Captures the cache's event lines — the ones that report a read, a hit or an eviction — so a test can assert
/// what the cache did, e.g. that one page reads once and is then a hit.
/// </summary>
internal sealed class ImagingLogCapture : Logger.ILogListener
{
    private static readonly string[] sEventPrefixes = ["read [", "hit [", "evict ["];

    private readonly ConcurrentQueue<string> _messages = new();

    public void OnLog(LogEntry item)
    {
        if (item.Tag.ToString() != nameof(ImageMemoryCache) || item.Message is not string message)
        {
            return;
        }

        if (sEventPrefixes.Any(prefix => message.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _messages.Enqueue(message);
        }
    }

    /// <summary>
    /// The captured messages that mention <paramref name="uri"/>, in order. Callers use a uri of their own so
    /// that history replayed by <c>Logger.AddListener</c> or other tests cannot be mistaken for theirs.
    /// </summary>
    public string[] EventsFor(string uri)
    {
        return [.. _messages.Where(message => message.Contains(uri, StringComparison.Ordinal))];
    }
}
