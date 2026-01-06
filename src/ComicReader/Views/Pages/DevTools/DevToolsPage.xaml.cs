// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Misc;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.DataModels;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.DevTools;

internal sealed partial class DevToolsPage : BasePage
{
    private const string TAG = nameof(DevToolsPage);

    public DevToolsPage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        GetMainPageAbility().SetTitle("Dev tools");
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Repair });
    }

    protected override void OnResume()
    {
        base.OnResume();

        SetResult(null);
        RestoreConfig();
    }

    //
    // Events
    //

    private void CrashAppButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        throw new InvalidOperationException();
    }

    private void TriggerAssertFailureButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Logger.AssertNotReachHere("MockAssertFailure");
    }

    private void OnCommonConfigsApplyClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string configs = TbCommonConfigs.Text;
        try
        {
            DebugSwitchModel.Instance.SaveConfigFromJson(configs);
        }
        catch (Exception ex)
        {
            SetResult(ex.ToString());
            return;
        }

        SetResult("Successfully applied");
        RestoreConfig();
    }

    private void OnCommonConfigsRestoreClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RestoreConfig();
    }

    private void OnShowDialogOnActiveWindowClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
        {
            await Task.Delay(3000);
            DialogOptions options = new DialogOptions.Builder()
                .SetContent("This is a test dialog")
                .SetPrimaryButtonText("Primary")
                .SetSecondaryButtonText("Secondary")
                .Build();
            DialogResult result = await DialogUtils.EnqueueDialogAsync(options);
            SetResult($"Show dialog result: {result.Result}");
        });
    }

    private void OnPrintMemoryLeakReportClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetResult(MemoryLeakTracker.GenerateReport());
    }

    private void ResetPurchaseStatusButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        PurchaseManager.MockDonorStatus(false);
    }

    private void BecomeADonorButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        PurchaseManager.MockDonorStatus(true);
    }

    private void DeveloperModeToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugUtils.DeveloperMode = DeveloperModeToggleSwitch.IsOn;
    }

    private void SentryToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugUtils.SentryEnabled = SentryToggleSwitch.IsOn;
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }

    private void SetResult(string? result)
    {
        if (result == null || result.Length == 0)
        {
            result = "Operation result shows here";
        }

        result = $"{DateTimeOffset.Now:yyyy/M/d HH:mm:ss.fff}\n{result.TrimEnd()}";
        TbOperationResult.Text = result;
    }

    private void RestoreConfig()
    {
        TbCommonConfigs.Text = DebugSwitchModel.Instance.GetConfigAsJson();
        DeveloperModeToggleSwitch.IsOn = DebugUtils.DeveloperMode;
        SentryToggleSwitch.IsOn = DebugUtils.SentryEnabled;
    }
}
