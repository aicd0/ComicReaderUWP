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
using ComicReaderUWP.Data.Models.Misc;

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
        ViewModel.Initialize();

        GlobalEvent.Instance.ComicUpdated.Observe(this, _ =>
        {
            ViewModel.UpdateStatistics();
        });

        GeneralSettingsSection.Initialize(ViewModel.Shared);
        ImageSourceSettingsSection.Initialize(this, ViewModel.Shared);
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
        });
    }

    //
    // Events
    //

    private void OnHistoryClearAllClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(() => ComicHistoryItemModel.ClearAsync());
        ViewModel.IsClearHistoryEnabled = false;
    }

    private void OnSendFeedbackButtonClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var uri = new Uri(StaticStringResources.SEND_FEEDBACK_URL);
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

    private void LicenseHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("License.txt", StaticStringResources.LICENSE);
    }

    private void GithubHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        CoroutineUtils.Run(async () => await Windows.System.Launcher.LaunchUriAsync(new Uri(StaticStringResources.GITHUB_REPO_URL)));
    }

    private void PrivacyPolicyHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        CoroutineUtils.Run(async () => await Windows.System.Launcher.LaunchUriAsync(new Uri(StaticStringResources.PRIVACY_POLICY_URL)));
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
        string author = "aicd0";
        string aboutCopyright = StringResourceProvider.Instance.AboutCopyright;
        aboutCopyright = aboutCopyright.Replace("$author", author);

        StringBuilder aboutTextBuilder = new();
        aboutTextBuilder
            .Append(StringResourceProvider.Instance.AppDisplayName)
            .Append(' ')
            .Append(EnvironmentProvider.Instance.GetHostVersion())
            .AppendLine()
            .Append("SDK ")
            .Append(EnvironmentProvider.Instance.GetSDKVersion())
            .AppendLine()
            .Append(aboutCopyright);
        ViewModel.AboutText = aboutTextBuilder.ToString();
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
