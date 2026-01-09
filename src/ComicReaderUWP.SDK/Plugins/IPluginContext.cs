// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Database.KV;
using ComicReaderUWP.SDK.DataModels;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Common;
using ComicReaderUWP.SDK.Plugins.Property;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins;

public interface IPluginContext
{
    string ResourceFolderPath { get; }

    IKVDatabase GetKVDatabase();

    Task Busy(Func<Task> action);

    Task<DialogResult> EnqueueDialogAsync(DialogOptions options);

    Task<DialogResult> EnqueueDialogAsync(int windowId, DialogOptions options);

    Task<DialogResult> EnqueueDialogAsync(ContentDialog dialog);

    Task<DialogResult> EnqueueDialogAsync(int windowId, ContentDialog dialog);

    Task<IComicModel?> GetComic(long id);

    Task<IEnumerable<long>> SearchComics(string filterExpression);

    void RegisterComicVirtualProperty(IVirtualProperty<IComicModel> property);

    void SetMainPageMoreMenuItemCreator(ICommonMenuItemCreator? creator);

    void SetComicMenuItemCreator(IComicMenuItemCreator? creator);

    void SetComicEditedHandler(IComicEditedHandler? handler);
}
