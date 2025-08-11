// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Navigation;

internal interface INavigationPageAbility : IPageAbility
{
    public delegate void CommonEventHandler();
    public delegate void InfoPaneToggledEventHandler(bool toggled);
    public delegate void GridViewModeChangedEventHandler(bool enabled);
    public delegate void FavoriteChangedEventHandler(bool isFavorite);
    public delegate void ReaderSettingsChangedEventHandler(ReaderSettingDataModel settings);
    public delegate void SearchTextChangeEventHandler(string text);

    void SetReaderSettings(ReaderSettingDataModel settings);

    void SetExternalComic(bool isExternal);

    void SetFavorite(bool isFavorite);

    void SetGridViewMode(bool enabled);

    void SetInfoPaneOpened(bool isOpened);

    bool GetIsSidePaneOpen();

    void SetIsSidePaneOpen(bool isOpen);

    void SetSearchBox(string text);

    void RegisterLeavingHandler(Page owner, CommonEventHandler handler);

    void RegisterRefreshHandler(Page owner, CommonEventHandler handler);

    void RegisterInfoPaneToggledHandler(Page owner, InfoPaneToggledEventHandler handler);

    void RegisterGridViewModeChangedHandler(Page owner, GridViewModeChangedEventHandler handler);

    void RegisterReaderSettingsChangedEventHandler(Page owner, ReaderSettingsChangedEventHandler handler);

    void RegisterFavoriteChangedEventHandler(Page owner, FavoriteChangedEventHandler handler);

    void RegisterSearchTextChangeHandler(Page owner, SearchTextChangeEventHandler handler);
}
