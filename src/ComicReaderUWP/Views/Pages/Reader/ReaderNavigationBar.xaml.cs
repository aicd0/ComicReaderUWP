// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Views.Pages.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderNavigationBar : BaseUserControl
{
    public delegate void GridViewModeChangedEventHandler(bool enabled);
    public event GridViewModeChangedEventHandler? GridViewModeChanged;

    public delegate void FavoriteChangedEventHandler(bool isFavorite);
    public event FavoriteChangedEventHandler? FavoriteChanged;

    public delegate void ReaderSettingsChangedEventHandler(ReaderSettingsModel settings);
    public event ReaderSettingsChangedEventHandler? ReaderSettingsChanged;

    public delegate void InfoPaneExpandedEventHandler();
    public event InfoPaneExpandedEventHandler? InfoPaneExpanded;

    private bool _isFavorite = false;

    public ReaderNavigationBar()
    {
        InitializeComponent();
    }

    public void SetWindowId(int windowId)
    {
        MainReaderSettingPanel.SetWindowId(windowId);
    }

    public void SetFavorite(bool isFavorite)
    {
        _isFavorite = isFavorite;
        FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
        string toolTip = isFavorite ? StringResourceProvider.Instance.RemoveFromFavorites :
            StringResourceProvider.Instance.AddToFavorites;
        ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
        FavoriteChanged?.Invoke(isFavorite);
    }

    public void SetExternalComic(bool isExternalComic)
    {
        AbbAddToFavorite.IsEnabled = !isExternalComic;
    }

    public void SetGridViewMode(bool enabled)
    {
        AbtbPreviewButton.IsChecked = enabled;
    }

    public void SetReaderSettings(ComicModel comic)
    {
        MainReaderSettingPanel.SetComic(comic);
    }

    private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
    {
        SetFavorite(!_isFavorite);
    }

    private void OnComicInfoClick(object sender, RoutedEventArgs e)
    {
        InfoPaneExpanded?.Invoke();
    }

    private void AbtbPreviewButton_Checked(object sender, RoutedEventArgs e)
    {
        GridViewModeChanged?.Invoke(true);
    }

    private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
    {
        GridViewModeChanged?.Invoke(false);
    }

    private void ReaderSettingFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        args.Cancel = MainReaderSettingPanel.ActionInProgress;
    }

    private void MainReaderSettingPanel_DataChanged(ReaderSettingsModel model)
    {
        ReaderSettingsChanged?.Invoke(model);
    }
}
