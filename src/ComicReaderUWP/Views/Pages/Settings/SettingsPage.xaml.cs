// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.Views.Dialogs.ChooseLocation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class SettingsPage : BasePage
{
    private SettingsPageViewModel ViewModel { get; } = new();

    public SettingsPage()
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

        ViewModel.Shared.WindowId = WindowId;
        ViewModel.Shared.ActionHandler = PageActionHandler;
        ViewModel.Initialize(this);
        GeneralSettingsSection.Initialize(ViewModel.Shared);
        ReaderSettingsSection.Initialize(ViewModel.Shared);
        PluginSettingsSection.Initialize(ViewModel.Shared);
        AdvancedSettingsSection.Initialize(ViewModel.Shared);
        ViewModel.Shared.UpdateStarted += Update;
        ViewModel.Shared.Update();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ViewModel.Shared.UpdateStarted -= Update;
    }

    private void Update()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            UpdateFeedback();
            UpdateAbout();
            UpdateDebugInformation();
            ViewModel.IsDonor = PurchaseManager.IsDonor;
        });
    }

    //
    // Events
    //

    private void ChooseLocationsClick(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var dialog = new ChooseLocationsDialog(WindowId);
            await dialog.ShowAsync(WindowId);
        });
    }

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(() => ComicHistoryItemModel.ClearAsync());
        ViewModel.IsClearHistoryEnabled = false;
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
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

    private void AppearanceRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetAppearance(((RadioButtons)sender).SelectedIndex);
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

    private void DonationButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            PurchaseManager.OperationResult result = await PurchaseManager.PurchaseDonor(WindowId);
            if (!result.Successful)
            {
                await DialogUtils.EnqueueDialogAsync(WindowId, new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Error)
                    .SetContent(result.ErrorMessage)
                    .Build());
                return;
            }

            ViewModel.IsDonor = PurchaseManager.IsDonor;
        }));
    }

    private void DonationAlreadyPurchasedHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            PurchaseManager.OperationResult result = await PurchaseManager.UpdatePurchaseStatus(WindowId);
            if (!result.Successful)
            {
                await DialogUtils.EnqueueDialogAsync(WindowId, new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Error)
                    .SetContent(result.ErrorMessage)
                    .Build());
                return;
            }

            if (!PurchaseManager.IsDonor)
            {
                await DialogUtils.EnqueueDialogAsync(WindowId, new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Error)
                    .SetContent(StringResourceProvider.Instance.PurchaseFailureMessage)
                    .Build());
                return;
            }

            ViewModel.IsDonor = PurchaseManager.IsDonor;
        }));
    }

    private void LicenseHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("License.txt", StaticStringResources.LICENSE);
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

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }
}
