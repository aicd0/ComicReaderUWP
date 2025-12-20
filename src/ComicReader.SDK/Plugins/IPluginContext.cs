// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Plugins.Comic;
using ComicReader.SDK.Plugins.Menu;

namespace ComicReader.SDK.Plugins;

public interface IPluginContext
{
    KVDatabaseMethod GetKVDatabase();

    Task<IComicModel?> GetComicById(long id);

    Task<IEnumerable<long>> SearchComics(string filterExpression);

    void RegisterMainPageMoreMenuItem(IMenuItem item);

    void RegisterComicEditedHandler(IComicEditedHandler handler);
}
