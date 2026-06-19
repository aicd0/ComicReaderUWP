// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.SDK.Models;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class PluginSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsSharedViewModel _shared = new();
    public SettingsSharedViewModel Shared
    {
        get => _shared;
        private set
        {
            _shared = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shared)));
        }
    }

    private bool _noPlugins = false;
    public bool NoPlugins
    {
        get => _noPlugins;
        set
        {
            _noPlugins = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoPlugins)));
        }
    }

    private bool _applyOnNextLaunchVisible = false;
    public bool ApplyOnNextLaunchVisible
    {
        get => _applyOnNextLaunchVisible;
        set
        {
            _applyOnNextLaunchVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ApplyOnNextLaunchVisible)));
        }
    }

    public readonly ObservableCollection<PluginItemViewModel> Plugins = [];

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
    }

    public void UpdatePlugins()
    {
        IEnumerable<PluginContext> plugins = PluginManager.Instance.GetAllPlugins();
        List<PluginItemViewModel> pluginItems = [];
        foreach (PluginContext plugin in plugins)
        {
            pluginItems.Add(new()
            {
                Name = plugin.Name,
                Publisher = plugin.Publisher,
                Description = plugin.Description,
                Icon = plugin.Icon ?? new FontIconSource() { Glyph = "\uE74C", FontSize = 20 },
                Version = plugin.Version,
                Location = plugin.PluginFilePath,
                Status = PluginStatusToString(plugin.Status),
                RequestOperationMenuItems = CreatePluginOperationMenuItems,
            });
        }

        DiffUtils.UpdateCollection(Plugins, pluginItems, (a, b) => a.Name == b.Name, (a, b) =>
        {
            a.Publisher = b.Publisher;
            a.Description = b.Description;
            a.Icon = b.Icon;
            a.Version = b.Version;
            a.Location = b.Location;
            a.Status = b.Status;
            a.RequestOperationMenuItems = b.RequestOperationMenuItems;
        });
        NoPlugins = Plugins.Count == 0;
    }

    private List<BaseMenuFlyoutItemModel> CreatePluginOperationMenuItems(PluginItemViewModel pluginItem)
    {
        string pluginName = pluginItem.Name;
        PluginContext? plugin = PluginManager.Instance.GetPlugin(pluginName);
        if (plugin is null)
        {
            return [];
        }

        string pluginFilePath = plugin.PluginFilePath;
        if (!File.Exists(pluginFilePath))
        {
            return [
                new SimpleMenuFlyoutItemModel()
                {
                    Text = StringResourceProvider.Instance.None,
                    IsEnabled = false,
                }
            ];
        }

        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Enable,
            IsEnabled = !PluginManager.Instance.IsPluginEnabled(pluginName),
            Click = () =>
            {
                PluginManager.Instance.SetPluginEnabled(pluginName, true);
                ApplyOnNextLaunchVisible = true;
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Disable,
            IsEnabled = PluginManager.Instance.IsPluginEnabled(pluginName),
            Click = () =>
            {
                PluginManager.Instance.SetPluginEnabled(pluginName, false);
                ApplyOnNextLaunchVisible = true;
            },
        });

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Remove,
            Icon = new FontIconSource() { Glyph = "\uE74D" },
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    string pluginList = string.Join('\n',
                        PluginManager.Instance.GetAllPlugins()
                            .Where(p => p.PluginFilePath == pluginFilePath)
                            .Select(p => p.Name));
                    DialogOptions options = new DialogOptions.Builder()
                        .SetTitle(StringResourceProvider.Instance.Warning)
                        .SetContent(StringResourceProvider.Instance.RemovePluginsConfirmation.Replace("$plugins", pluginList))
                        .SetPrimaryButtonText(StringResourceProvider.Instance.Remove)
                        .SetSecondaryButtonText(StringResourceProvider.Instance.Cancel)
                        .Build();
                    DialogResult result = await DialogUtils.EnqueueDialogAsync(Shared.WindowId, options);
                    if (result == DialogResult.Primary)
                    {
                        File.Delete(pluginFilePath);
                        ApplyOnNextLaunchVisible = true;
                    }
                });
            },
        });

        return items;
    }

    private static string PluginStatusToString(PluginStatusEnum status)
    {
        return status switch
        {
            PluginStatusEnum.NotInitialized => StringResourceProvider.Instance.Disabled,
            PluginStatusEnum.Initialized => string.Empty,
            PluginStatusEnum.Error => StringResourceProvider.Instance.Error,
            _ => StringResourceProvider.Instance.Error,
        };
    }
}
