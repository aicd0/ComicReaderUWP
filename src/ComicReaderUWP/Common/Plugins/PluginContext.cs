// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.SDK.Plugins;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Common;
using ComicReaderUWP.SDK.Plugins.Menu;
using ComicReaderUWP.SDK.Plugins.Property;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Plugins;

internal partial class PluginContext : IPluginContext
{
    private const string TAG = nameof(PluginContext);

    public delegate void StatusChangedEventHandler(PluginStatusEnum newStatus);
    public event StatusChangedEventHandler? StatusChanged;

    public string Name => _pluginName;
    public string PluginFilePath { get; init; }
    public string Publisher => _plugin.Publisher;
    public string Version => $"{_plugin.MajorVersion}.{_plugin.MinorVersion}";
    public bool IsActive => Status == PluginStatusEnum.Initialized;

    private PluginStatusEnum _status = PluginStatusEnum.NotInitialized;
    public PluginStatusEnum Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke(_status);
            }
        }
    }

    private readonly IPlugin _plugin;
    private readonly string _pluginName;
    private readonly PluginLoader.PluginFileLoadResult _loadContext;
    private readonly Lazy<ILogger> _logger;
    private readonly Lazy<IRegistryDatabase> _registryDatabase;
    private readonly Dictionary<string, IVirtualProperty<IComicModel>> _comicVirtualProperties = [];
    private ICommonMenuItemCreator? _mainPageMoreMenuItemCreator = null;
    private IComicMenuItemCreator? _comicMenuItemCreator = null;
    private IComicEditedHandler? _comicEditedHandlers = null;

    public PluginContext(IPlugin plugin, string pluginFilePath, PluginLoader.PluginFileLoadResult loadContext)
    {
        PluginFilePath = pluginFilePath;
        _plugin = plugin;
        _pluginName = plugin.Name;
        _loadContext = loadContext;
        _logger = new Lazy<ILogger>(() => new PluginLogger(_pluginName));
        _registryDatabase = new Lazy<IRegistryDatabase>(() => AppDB.PluginRegistry(_pluginName));
    }

    //
    // Internal API
    //

    public void Initialize()
    {
        if (Status != PluginStatusEnum.NotInitialized)
        {
            Logger.F(TAG, $"({_pluginName}) Initialize: Plugin already initialized or in error state");
            return;
        }

        if (SafeAction(() => _plugin.Initialize(this)))
        {
            Status = PluginStatusEnum.Initialized;
        }
    }

    public IEnumerable<IVirtualProperty<IComicModel>> GetAllComicVirtualProperties()
    {
        if (!IsActive)
        {
            return [];
        }

        return _comicVirtualProperties.Values;
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetMainPageMoreMenuItems(IUIContext uiContext)
    {
        if (!IsActive)
        {
            return [];
        }

        ICommonMenuItemCreator? creator = _mainPageMoreMenuItemCreator;
        if (creator is null)
        {
            return [];
        }

        return [.. SafeAction(() => creator.CreateMenuItems(uiContext), []).Select(CreateHostMenuFlyoutItem)];
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetComicMenuItems(IUIContext uiContext, IComicModel primary, IEnumerable<IComicModel> selection)
    {
        if (!IsActive)
        {
            return [];
        }

        IComicMenuItemCreator? creator = _comicMenuItemCreator;
        if (creator is null)
        {
            return [];
        }

        return [.. SafeAction(() => creator.CreateMenuItems(uiContext, primary, selection), []).Select(CreateHostMenuFlyoutItem)];
    }

    public void DispatchComicEditedEvent(IComicModel comic)
    {
        if (!IsActive)
        {
            return;
        }

        SafeAction(() => _comicEditedHandlers?.ComicEdited(comic));
    }

    //
    // IPluginContext Implementation
    //

    string IPluginContext.ResourceFolderPath => _loadContext.ResourceFolderPath;

    ILogger IPluginContext.Logger => _logger.Value;

    IRegistryDatabase IPluginContext.RegistryDatabase => _registryDatabase.Value;

    Task IPluginContext.Busy(Func<Task> action)
    {
        return BusyStateManager.WithBusyState(action);
    }

    Task<DialogResult> IPluginContext.EnqueueDialogAsync(DialogOptions options)
    {
        return DialogUtils.EnqueueDialogAsync(options);
    }

    Task<DialogResult> IPluginContext.EnqueueDialogAsync(int windowId, DialogOptions options)
    {
        return DialogUtils.EnqueueDialogAsync(windowId, options);
    }

    Task<DialogResult> IPluginContext.EnqueueDialogAsync(ContentDialog dialog)
    {
        return DialogUtils.EnqueueDialogAsync(dialog);
    }

    Task<DialogResult> IPluginContext.EnqueueDialogAsync(int windowId, ContentDialog dialog)
    {
        return DialogUtils.EnqueueDialogAsync(windowId, dialog);
    }

    async Task<IComicModel?> IPluginContext.GetComic(long id)
    {
        return await ComicModel.FromId(id, "PluginGetComicById");
    }

    async Task<IEnumerable<long>> IPluginContext.SearchComics(string filterExpression)
    {
        ICondition? filterCondition = ParseFilterExpression(filterExpression) ?? throw new InvalidExpressionException();
        List<long> ids = [];
        await ComicHandle.Enqueue("SearchComics", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            command.AppendCondition(filterCondition);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                ids.Add(idToken.GetValue());
            }

            return true;
        });

        return ids;
    }

    void IPluginContext.RegisterComicVirtualProperty(IVirtualProperty<IComicModel> property)
    {
        ArgumentNullException.ThrowIfNull(property, nameof(property));

        string name = property.Name;
        if (string.IsNullOrEmpty(name) || !VirtualPropertyNameRegex().IsMatch(name))
        {
            Logger.F(TAG, $"({_pluginName}) RegisterComicVirtualProperty: Invalid name '{name}'");
            return;
        }

        if (!_comicVirtualProperties.TryAdd(name, property))
        {
            Logger.F(TAG, $"({_pluginName}) RegisterComicVirtualProperty: Property '{name}' already registered");
        }
    }

    void IPluginContext.SetMainPageMoreMenuItemCreator(ICommonMenuItemCreator? creator)
    {
        _mainPageMoreMenuItemCreator = creator;
    }

    void IPluginContext.SetComicMenuItemCreator(IComicMenuItemCreator? creator)
    {
        _comicMenuItemCreator = creator;
    }

    void IPluginContext.SetComicEditedHandler(IComicEditedHandler? handler)
    {
        _comicEditedHandlers = handler;
    }

    //
    // Helpers
    //

    private bool SafeAction(Action? action)
    {
        if (action is null)
        {
            return true;
        }

        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, $"Unhandled exception thrown from plugin: {_pluginName}", ex);
            Status = PluginStatusEnum.Error;
            return false;
        }
    }

    private T SafeAction<T>(Func<T>? action, T defaultValue)
    {
        if (action is null)
        {
            return defaultValue;
        }

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Logger.F(TAG, $"Unhandled exception thrown from plugin: {_pluginName}", ex);
            Status = PluginStatusEnum.Error;
            return defaultValue;
        }
    }

    private BaseMenuFlyoutItemModel CreateHostMenuFlyoutItem(IMenuItem item)
    {
        return item switch
        {
            SimpleMenuItem simpleMenuItem => new SimpleMenuFlyoutItemModel()
            {
                Text = simpleMenuItem.Text,
                Glyph = simpleMenuItem.Glyph,
                IsEnabled = simpleMenuItem.IsEnabled,
                Click = () => SafeAction(simpleMenuItem.Click),
            },
            SeparatorMenuItem => new SeparatorMenuFlyoutItemModel(),
            ToggleMenuItem toggleMenuItem => new ToggleMenuFlyoutItemModel()
            {
                Text = toggleMenuItem.Text,
                IsChecked = toggleMenuItem.IsChecked,
                Click = () => SafeAction(toggleMenuItem.Click),
            },
            SubItemMenuItem subItemMenuItem => new SubItemMenuFlyoutItemModel()
            {
                Text = subItemMenuItem.Text,
                Glyph = subItemMenuItem.Glyph,
                Items = [.. subItemMenuItem.Items.Select(CreateHostMenuFlyoutItem)],
            },
            _ => throw new NotSupportedException($"Unsupported menu item type: {item.GetType().FullName}"),
        };
    }

    private static ICondition? ParseFilterExpression(string expression)
    {
        Common.Expression.Filter.ExpressionToken token;
        try
        {
            token = ExpressionParser.ParseFilter(expression);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        ICondition condition;
        try
        {
            condition = Common.Expression.Filter.Sql.SQLGenerator.CreateQuery(token, new ComicFilterSQLProvider());
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        return condition;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial Regex VirtualPropertyNameRegex();
}
