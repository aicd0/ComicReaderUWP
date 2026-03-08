// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.DataModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class AdvancedSettingsView : BaseUserControl
{
    public AdvancedSettingsViewModel ViewModel { get; } = new();

    public AdvancedSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void OnDebugModeToggled(object sender, RoutedEventArgs e)
    {
        bool debugMode = TsDebugMode.IsOn;
        if (ViewModel.Shared.DebugMode == debugMode)
        {
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            if (debugMode)
            {
                DialogOptions options = new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Warning)
                    .SetContent(StringResourceProvider.Instance.DebugModeWarning)
                    .SetPrimaryButtonText(StringResourceProvider.Instance.Proceed)
                    .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                    .Build();
                DialogResult result = await DialogUtils.EnqueueDialogAsync(ViewModel.Shared.WindowId, options);
                if (result.Result != ContentDialogResult.Primary)
                {
                    ViewModel.Shared.DebugMode = false;
                    return;
                }
            }

            ViewModel.Shared.DebugMode = debugMode;
            DebugUtils.DebugMode = debugMode;
        });
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

        er.DisplayErrorMessage(ViewModel.Shared.ActionHandler);
    }

    private void ResetAllSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            DialogOptions options = new DialogOptions.Builder()
                .SetTitle(StringResourceProvider.Instance.ResetAllSettings)
                .SetContent(StringResourceProvider.Instance.ResetAllSettingsMessage)
                .SetPrimaryButtonText(StringResourceProvider.Instance.Proceed)
                .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                .Build();
            DialogResult result = await DialogUtils.EnqueueDialogAsync(ViewModel.Shared.WindowId, options);
            if (result.Result == ContentDialogResult.Primary)
            {
                AppSettingsModel.Instance.Reset();
                ViewModel.Shared.Update();
            }
        });
    }
}
