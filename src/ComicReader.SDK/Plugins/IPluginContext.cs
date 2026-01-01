// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Database.KV;
using ComicReader.SDK.DataModels;
using ComicReader.SDK.Plugins.Comic;
using ComicReader.SDK.Plugins.Common;
using ComicReader.SDK.Plugins.Property;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.SDK.Plugins;

public interface IPluginContext
{
    IKVDatabase GetKVDatabase();

    Task<IComicModel?> GetComicById(long id);

    Task<IEnumerable<long>> SearchComics(string filterExpression);

    void RegisterComicVirtualProperty(IVirtualProperty<IComicModel> property);

    Task WithBusyState(Func<Task> action);

    Task<ContentDialogResult> EnqueueDialogAsync(DialogOptions options);

    void SetMainPageMoreMenuItemCreator(ICommonMenuItemCreator? creator);

    void SetComicMenuItemCreator(IComicMenuItemCreator? creator);

    void SetComicEditedHandler(IComicEditedHandler? handler);
}
