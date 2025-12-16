// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Views.Pages.Main;

internal interface INavigationPageAbility : IPageAbility
{
    public delegate void CommonEventHandler();
    public delegate void GridViewModeChangedEventHandler(bool enabled);
    public delegate void FavoriteChangedEventHandler(bool isFavorite);
    public delegate void ReaderSettingsChangedEventHandler(ReaderSettingDataModel settings);
    public delegate void SearchTextChangeEventHandler(string text);

    void SetReaderSettings(ComicModel comic);

    void SetExternalComic(bool isExternal);

    void SetFavorite(bool isFavorite);

    void SetGridViewMode(bool enabled);

    void SetSearchBox(string text);

    void SetFullscreenButtonVisible(bool visible);

    void RegisterRefreshHandler(ILifecycleOwner owner, CommonEventHandler handler);

    void RegisterExpandInfoPaneHandler(ILifecycleOwner owner, CommonEventHandler handler);

    void RegisterGridViewModeChangedHandler(ILifecycleOwner owner, GridViewModeChangedEventHandler handler);

    void RegisterReaderSettingsChangedEventHandler(ILifecycleOwner owner, ReaderSettingsChangedEventHandler handler);

    void RegisterFavoriteChangedEventHandler(ILifecycleOwner owner, FavoriteChangedEventHandler handler);

    void RegisterSearchTextChangeHandler(ILifecycleOwner owner, SearchTextChangeEventHandler handler);
}
