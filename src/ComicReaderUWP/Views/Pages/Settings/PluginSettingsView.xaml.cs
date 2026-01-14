// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.SDK.Common.Utils;

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

    protected override void OnStart()
    {
        base.OnStart();
        ObserveData();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void ObserveData()
    {
        PluginManager.PluginsChanged.ObserveSticky(this, _ =>
        {
            ViewModel.UpdatePlugins();
        });
    }

    private void OpenPluginsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
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
            }

            er.DisplayErrorMessage(ViewModel.Shared.ActionHandler);
        });
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
