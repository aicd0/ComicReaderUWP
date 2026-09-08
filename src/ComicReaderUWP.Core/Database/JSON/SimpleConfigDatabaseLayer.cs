// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Core.Database.JSON;

public class SimpleConfigDatabaseLayer(string filename) : IConfigBackingLayer
{
    private readonly string _filename = filename;
    private readonly ITaskDispatcher _queue = TaskDispatcher.Factory.NewQueue($"{nameof(JsonDatabase<>)}#{filename}");

    public string? ReadConfig()
    {
        return SimpleConfigDatabase.Instance.TryGetConfig(_filename);
    }

    public void WriteConfig(string value)
    {
        _queue.Submit(() =>
        {
            SimpleConfigDatabase.Instance.TryPutConfig(_filename, value);
        });
    }
}
