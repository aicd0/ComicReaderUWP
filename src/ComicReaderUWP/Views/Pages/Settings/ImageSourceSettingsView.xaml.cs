// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.Views.Dialogs.ChooseLocation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class ImageSourceSettingsView : BaseUserControl
{
    public ImageSourceSettingsViewModel ViewModel { get; } = new();

    public ImageSourceSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(ILifecycleOwner owner, SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);

        ComicHandle.IsScanningLibraryLiveData.ObserveSticky(owner, isScanning =>
        {
            ViewModel.IsRescanning = isScanning;
        });
    }

    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            int windowId = ViewModel.Shared.WindowId;
            var dialog = new ChooseLocationsDialog(windowId);
            await dialog.ShowAsync(windowId);
        });
    }

    private void ScanOnLaunchCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetScanOnLaunch(isChecked.Value);
        }
    }

    private void RemoveUnreachableCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetRemoveUnreachableComics(isChecked.Value);
        }
    }

    private void PromptBeforeRemovingComicsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetPromptBeforeRemovingComics(isChecked.Value);
        }
    }

    private void OnRescanFilesClicked(object sender, RoutedEventArgs e)
    {
        ComicModel.RescanLibrary("OnRescanFilesClicked");
    }

    private void EditImportExclusionListButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            ErrorResult err = await ComicImportExclusionModel.Instance.EditWithNotepad();
            err.DisplayErrorMessage(ViewModel.Shared.ActionHandler);
        });

    }

    private void ShowHiddenComicButton_Click(object sender, RoutedEventArgs e)
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, $"{ComicSQLProviderUtils.VAR_HIDDEN}:1");
        ActionModel action = ActionModel.Builder.Create(OpenTabProvider.NAME)
            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
            .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
            .Build();
        ViewModel.Shared.ActionHandler.HandleNoResult(action);
    }

    private void DefaultArchiveCodePageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetDefaultArchiveCodePage(((ComboBox)sender).SelectedIndex);
    }
}
