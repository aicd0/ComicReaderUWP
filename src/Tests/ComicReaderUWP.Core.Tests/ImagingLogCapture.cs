// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Core.Tests;

/// <summary>
/// Captures the <c>Imaging/Cache</c> lines so a test can assert what the cache reported, e.g. that one page
/// reads once and is then a hit.
/// </summary>
internal sealed class ImagingLogCapture : Logger.ILogListener
{
    private readonly ConcurrentQueue<string> _messages = new();

    public void OnLog(Logger.LogItem item)
    {
        if (item.Tag.ToString() == "Imaging/Cache" && item.Message is not null)
        {
            _messages.Enqueue(item.Message);
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
