// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Property;
using ComicReaderUWP.SDK.Plugins.UI.Menu;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins;

public delegate void ComicEditedEventHandler(IComicModel comic);

public interface IPluginContext
{
    event ComicEditedEventHandler? ComicEdited;

    string ResourceFolderPath { get; }

    ILogger Logger { get; }

    IRegistryDatabase RegistryDatabase { get; }

    IComicMenuItemCreator? ComicMenuItemCreator { get; set; }

    ICommonMenuItemCreator? MainPageMoreMenuItemCreator { get; set; }

    Task WithBusyState(Func<Task> action);

    Task<DialogResult> EnqueueDialog(DialogOptions options);

    Task<DialogResult> EnqueueDialog(int windowId, DialogOptions options);

    Task<DialogResult> EnqueueDialog(ContentDialog dialog);

    Task<DialogResult> EnqueueDialog(int windowId, ContentDialog dialog);

    Task<IComicModel?> GetComic(long id);

    Task<IEnumerable<long>> SearchComics(string filterExpression);

    void RegisterComicVirtualProperty(IVirtualProperty<IComicModel> property);
}
