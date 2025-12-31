// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.Actions;
using ComicReader.Common.Localization;
using ComicReader.Common.Plugins;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.Algorithm;

namespace ComicReader.Views.Pages.Settings;

internal partial class PluginSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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
    public ActionHandler PageActionHandler { get; private set; } = ActionHandler.Dummy;

    public void Initialize(ActionHandler actionHandler)
    {
        PageActionHandler = actionHandler;
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
                Version = plugin.Version,
                Publisher = plugin.Publisher,
                Status = PluginStatusToString(plugin.Status),
                Location = plugin.AssemblyPath,
                RequestOperationMenuItems = CreatePluginOperationMenuItems,
            });
        }

        DiffUtils.UpdateCollection(Plugins, pluginItems, (a, b) => a.Name == b.Name, (a, b) =>
        {
            a.Status = b.Status;
            a.Location = b.Location;
            a.Publisher = b.Publisher;
            a.Version = b.Version;
            a.RequestOperationMenuItems = b.RequestOperationMenuItems;
        });
        NoPlugins = Plugins.Count == 0;
    }

    private List<BaseMenuFlyoutItemModel> CreatePluginOperationMenuItems(PluginItemViewModel pluginItem)
    {
        string pluginName = pluginItem.Name;
        PluginContext? plugin = PluginManager.Instance.GetPlugin(pluginName);
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Enable,
            IsEnabled = plugin is not null && !PluginManager.Instance.IsPluginEnabled(pluginName),
            Click = () =>
            {
                if (plugin is not null)
                {
                    PluginManager.Instance.SetPluginEnabled(pluginName, true);
                    ApplyOnNextLaunchVisible = true;
                }
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Disable,
            IsEnabled = plugin is not null && PluginManager.Instance.IsPluginEnabled(pluginName),
            Click = () =>
            {
                if (plugin is not null)
                {
                    PluginManager.Instance.SetPluginEnabled(pluginName, false);
                    ApplyOnNextLaunchVisible = true;
                }
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
