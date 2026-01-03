// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common.Expression;
using ComicReader.Common.Misc;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Database.KV;
using ComicReader.SDK.Database.SqlHelpers;
using ComicReader.SDK.DataModels;
using ComicReader.SDK.Plugins;
using ComicReader.SDK.Plugins.Comic;
using ComicReader.SDK.Plugins.Common;
using ComicReader.SDK.Plugins.Menu;
using ComicReader.SDK.Plugins.Property;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.Plugins;

internal partial class PluginContext(IPlugin plugin, string assemblyPath) : IPluginContext
{
    private const string TAG = nameof(PluginContext);

    public delegate void StatusChangedEventHandler(PluginStatusEnum newStatus);
    public event StatusChangedEventHandler? StatusChanged;

    public string Name => _pluginName;
    public string AssemblyPath => assemblyPath;
    public string Publisher => plugin.Publisher;
    public string Version => $"{plugin.MajorVersion}.{plugin.MinorVersion}";

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

    public bool IsActive => Status == PluginStatusEnum.Initialized;

    private readonly string _pluginName = plugin.Name;

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

        if (SafeAction(() => plugin.Initialize(this)))
        {
            Status = PluginStatusEnum.Initialized;
        }
    }

    //
    // Database API
    //

    IKVDatabase IPluginContext.GetKVDatabase()
    {
        return KVStore.Plugin(_pluginName);
    }

    //
    // Comics API
    //

    async Task<IComicModel?> IPluginContext.GetComicById(long id)
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

    //
    // Comic virtual property
    //

    private readonly Dictionary<string, IVirtualProperty<IComicModel>> _comicVirtualProperties = [];

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

    public IEnumerable<IVirtualProperty<IComicModel>> GetAllComicVirtualProperties()
    {
        if (!IsActive)
        {
            return [];
        }

        return _comicVirtualProperties.Values;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial Regex VirtualPropertyNameRegex();

    //
    // Common UI
    //

    Task IPluginContext.WithBusyState(Func<Task> action)
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

    //
    // Main Page More Menu Items
    //

    private ICommonMenuItemCreator? _mainPageMoreMenuItemCreator = null;

    void IPluginContext.SetMainPageMoreMenuItemCreator(ICommonMenuItemCreator? creator)
    {
        _mainPageMoreMenuItemCreator = creator;
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

    //
    // Comic Menu Items
    //

    private IComicMenuItemCreator? _comicMenuItemCreator = null;

    void IPluginContext.SetComicMenuItemCreator(IComicMenuItemCreator? creator)
    {
        _comicMenuItemCreator = creator;
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

    //
    // Comic Edited Handlers
    //

    private IComicEditedHandler? _comicEditedHandlers = null;

    void IPluginContext.SetComicEditedHandler(IComicEditedHandler? handler)
    {
        _comicEditedHandlers = handler;
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
            Logger.F(TAG, $"({_pluginName}) SafeAction: Caught exception", ex);
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
            Logger.F(TAG, $"({_pluginName}) SafeAction: Unhandled plugin exception", ex);
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
}
