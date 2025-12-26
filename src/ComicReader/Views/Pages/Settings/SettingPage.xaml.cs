// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Localization;
using ComicReader.Common.Misc;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.Misc;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.DataModels;
using ComicReader.Views.Dialogs.ChooseLocation;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Settings;

internal sealed partial class SettingPage : BasePage
{
    private SettingPageViewModel ViewModel { get; } = new();

    public SettingPage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.Settings);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Setting });

        ViewModel.Initialize(this);
        PluginSettingsSection.Initialize(PageActionHandler);
        UpdateFeedback();
        UpdateAbout();
        UpdateDebugInformation();
    }

    //
    // Events
    //

    private void OnDebugModeToggled(object sender, RoutedEventArgs e)
    {
        bool debugMode = TsDebugMode.IsOn;
        if (ViewModel.DebugMode == debugMode)
        {
            return;
        }

        CoroutineUtils.Start(async () =>
        {
            if (debugMode)
            {
                DialogOptions options = new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Warning)
                    .SetContent(StringResourceProvider.Instance.DebugModeWarning)
                    .SetPrimaryButtonText(StringResourceProvider.Instance.Proceed)
                    .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                    .Build();
                ContentDialogResult result = await DialogUtils.EnqueueDialogAsync(WindowId, options);
                if (result == ContentDialogResult.None)
                {
                    ViewModel.DebugMode = false;
                    return;
                }
            }

            ViewModel.DebugMode = debugMode;
            DebugUtils.DebugMode = debugMode;
        });
    }

    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
        {
            var dialog = new ChooseLocationsDialog(WindowId);
            await dialog.ShowAsync(WindowId);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Start(() => ComicHistoryItemModel.ClearAsync());
        ViewModel.IsClearHistoryEnabled = false;
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
        {
            var uri = new Uri(@"https://github.com/aicd0/ComicReaderUWP/issues/new/choose");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        });
    }

    private void BackgroundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetBackground(((ComboBox)sender).SelectedIndex);
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetAppLanguage(((ComboBox)sender).SelectedIndex);
    }

    private void ShowHiddenComicButton_Click(object sender, RoutedEventArgs e)
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, $"{ComicSQLProviderUtils.VAR_HIDDEN}:1");
        GetMainPageAbility().OpenInNewTab(route);
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

    private void RestoreLastReadingPositionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetRestoreLastReadingPosition(isChecked.Value);
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
        ComicModel.UpdateAllComics("OnRescanFilesClicked");
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCache();
    }

    private void OnRefreshRandomSeedClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshRandomSeed();
    }

    private async void OnOpenUserDataFolderClick(object sender, RoutedEventArgs e)
    {
        string path = StorageLocation.LocalFolderPath;
        var er = EventRecorder.Create("OnOpenUserDataFolderClick");
        try
        {
            Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
            await Windows.System.Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception ex)
        {
            er.SetError(ex);
        }

        er.DisplayErrorMessage(PageActionHandler);
    }

    //
    // UI
    //

    private void UpdateFeedback()
    {
        string appName = StringResourceProvider.Instance.AppDisplayName;
        string contributionBeforeLink = StringResourceProvider.Instance.ContributionRunBeforeLink;
        contributionBeforeLink = contributionBeforeLink.Replace("$appname", appName);
        ContributionRunBeforeLink.Text = contributionBeforeLink;
        ContributionRunAfterLink.Text = StringResourceProvider.Instance.ContributionRunAfterLink;
    }

    private void UpdateAbout()
    {
        string appName = StringResourceProvider.Instance.AppDisplayName;
        AboutBuildVersionControl.Text = appName + " " + EnvironmentProvider.Instance.GetHostVersion();

        string author = "aicd0";
        string aboutCopyright = StringResourceProvider.Instance.AboutCopyright;
        aboutCopyright = aboutCopyright.Replace("$author", author);
        AboutCopyrightControl.Text = aboutCopyright;
    }

    private void UpdateDebugInformation()
    {
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        TbDebugInformation.Text = sb.ToString();
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }
}
