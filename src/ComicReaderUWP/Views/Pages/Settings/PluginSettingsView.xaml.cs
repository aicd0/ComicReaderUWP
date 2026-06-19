// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.SDK.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class PluginSettingsView : BaseUserControl
{
    public PluginSettingsViewModel ViewModel { get; } = new();

    public PluginSettingsView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            Bindings.StopTracking();
            Bindings.Update();
        };
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void ObserveData()
    {
        ObserveOptions options = new()
        {
            Sticky = true,
            PublishBehavior = LiveDataPublishBehavior.ResumeOnly,
        };

        PluginManager.PluginsChanged.Observe(this, _ =>
        {
            ViewModel.UpdatePlugins();
        }, options);
    }

    private void OpenPluginsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            string path = PluginManager.PluginsFolderPath;
            var er = EventRecorder.Create("OpenPluginsFolderClick");
            try
            {
                Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
                await Windows.System.Launcher.LaunchFolderAsync(folder);
            }
            catch (Exception ex)
            {
                er.SetError(ex);
                er.DisplayErrorMessage(ViewModel.Shared.ActionHandler);
            }
        });
    }

    private void InstallPluginButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            Windows.Storage.StorageFile? file = await FilePickerUtils.PickFile(ViewModel.Shared.WindowId, [".zip"]);
            if (file is null)
            {
                return;
            }

            if (AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).GetValueOrDefault(RegistryNames.SettingsKey.SHOW_INSTALL_PLUGIN_WARNING, true))
            {
                DialogOptions options = new DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Warning)
                    .SetContent(StringResourceProvider.Instance.InstallPluginWarning)
                    .SetPrimaryButtonText(StringResourceProvider.Instance.Proceed)
                    .SetSecondaryButtonText(StringResourceProvider.Instance.Cancel)
                    .Build();
                DialogResult result = await DialogUtils.EnqueueDialogAsync(ViewModel.Shared.WindowId, options);
                if (result != DialogResult.Primary)
                {
                    return;
                }

                AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).Set(RegistryNames.SettingsKey.SHOW_INSTALL_PLUGIN_WARNING, false);
            }

            string pluginsFolderPath = PluginManager.PluginsFolderPath;
            string dstFilePath = Path.Combine(pluginsFolderPath, file.Name);

            var er = EventRecorder.Create("InstallPluginClick");
            try
            {
                File.Copy(file.Path, dstFilePath, true);
            }
            catch (Exception ex)
            {
                er.SetError(ex);
                er.DisplayErrorMessage(ViewModel.Shared.ActionHandler);
                return;
            }

            ViewModel.ApplyOnNextLaunchVisible = true;
        });
    }

    private void DownloadPluginsFromGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () => await Windows.System.Launcher.LaunchUriAsync(new Uri(StaticStringResources.GITHUB_PLUGINS_REPO_URL)));
    }

    private void PluginItemMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not PluginItemViewModel item)
        {
            return;
        }

        List<BaseMenuFlyoutItemModel> menuItems = item.CreateOperationMenuItems();
        if (menuItems.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout()
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };
        foreach (BaseMenuFlyoutItemModel menuItem in menuItems)
        {
            flyout.Items.Add(menuItem.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe);
    }
}
