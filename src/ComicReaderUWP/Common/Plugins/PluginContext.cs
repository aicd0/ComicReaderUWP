// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.SDK.Plugins;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Common;
using ComicReaderUWP.SDK.Plugins.Property;
using ComicReaderUWP.SDK.Plugins.UI;
using ComicReaderUWP.SDK.Plugins.UI.Menu;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Plugins;

internal partial class PluginContext : IPluginContext
{
    private const string TAG = nameof(PluginContext);

    public delegate void StatusChangedEventHandler(PluginStatusEnum newStatus);
    public event StatusChangedEventHandler? StatusChanged;

    public IPlugin Plugin { get; init; }
    public string Name { get; init; }
    public string PluginFilePath { get; init; }
    public PluginFileLoadContext LoadContext { get; init; }
    public string Publisher => Plugin.Publisher;
    public string Version => Plugin.Version;
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

    public PluginContext(IPlugin plugin, string pluginFilePath, PluginFileLoadContext loadContext)
    {
        Plugin = plugin;
        PluginFilePath = pluginFilePath;
        Name = plugin.Name;
        LoadContext = loadContext;
        _logger = new Lazy<ILogger>(() => new PluginLogger(Name));
        _registryDatabase = new Lazy<IRegistryDatabase>(() => AppDB.PluginRegistry(Name));
    }

    //
    // Internal API
    //

    public void Initialize()
    {
        if (Status != PluginStatusEnum.NotInitialized)
        {
            Logger.F(TAG, $"({Name}) Initialize: Plugin already initialized or in error state");
            return;
        }

        if (SafeAction(() => Plugin.Initialize(this)))
        {
            Status = PluginStatusEnum.Initialized;
        }
    }

    public void DispatchComicEditedEvent(IComicModel comic)
    {
        if (!IsActive)
        {
            return;
        }

        SafeAction(() => _comicEditedEventHandler?.Invoke(comic));
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetMainPageMoreMenuItems(IWindowContext windowContext)
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

        return [.. SafeAction(() => creator.CreateMenuItems(windowContext), []).Select(CreateHostMenuFlyoutItem)];
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetComicMenuItems(IWindowContext windowContext, IComicModel primary, IEnumerable<IComicModel> selection)
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

        return [.. SafeAction(() => creator.CreateMenuItems(windowContext, primary, selection), []).Select(CreateHostMenuFlyoutItem)];
    }

    public IEnumerable<IVirtualProperty<IComicModel>> GetAllComicVirtualProperties()
    {
        if (!IsActive)
        {
            return [];
        }

        return [.. _comicVirtualProperties.Values];
    }

    public IReadOnlyList<ISidebarPageProvider> GetAllSidebarPageProviders()
    {
        if (!IsActive)
        {
            return [];
        }

        return [.. _sidebarPageProviders];
    }

    public void SetReadingComic(PluginWindowContext windowContext, IComicModel? comic)
    {
        if (!IsActive)
        {
            return;
        }

        windowContext.SetReadingComic(comic, SafeAction);
    }

    //
    // IPluginContext Implementation
    //

    private event ComicEditedEventHandler? _comicEditedEventHandler;

    private readonly Dictionary<string, IVirtualProperty<IComicModel>> _comicVirtualProperties = [];
    private readonly List<ISidebarPageProvider> _sidebarPageProviders = [];
    private readonly Lazy<ILogger> _logger;
    private readonly Lazy<IRegistryDatabase> _registryDatabase;
    private IComicMenuItemCreator? _comicMenuItemCreator = null;
    private ICommonMenuItemCreator? _mainPageMoreMenuItemCreator = null;

    event ComicEditedEventHandler? IPluginContext.ComicEdited
    {
        add => _comicEditedEventHandler += value;
        remove => _comicEditedEventHandler -= value;
    }

    CultureInfo IPluginContext.CurrentCulture => EnvironmentProvider.Instance.GetCurrentAppLanguageInfo();

    string IPluginContext.ResourceFolderPath => LoadContext.ResourceFolderPath;

    ILogger IPluginContext.Logger => _logger.Value;

    IRegistryDatabase IPluginContext.RegistryDatabase => _registryDatabase.Value;

    IComicMenuItemCreator? IPluginContext.ComicMenuItemCreator
    {
        get => _comicMenuItemCreator;
        set => _comicMenuItemCreator = value;
    }

    ICommonMenuItemCreator? IPluginContext.MainPageMoreMenuItemCreator
    {
        get => _mainPageMoreMenuItemCreator;
        set => _mainPageMoreMenuItemCreator = value;
    }

    Task IPluginContext.WithBusyState(Func<Task> action)
    {
        return BusyStateManager.WithBusyState(action);
    }

    Task<DialogResult> IPluginContext.EnqueueDialog(DialogOptions options)
    {
        return DialogUtils.EnqueueDialogAsync(options);
    }

    Task<DialogResult> IPluginContext.EnqueueDialog(ContentDialog dialog)
    {
        return DialogUtils.EnqueueDialogAsync(dialog);
    }

    async Task<IComicModel?> IPluginContext.GetComic(long id)
    {
        return await ComicModel.FromId(id);
    }

    async Task<IEnumerable<long>> IPluginContext.SearchComics(string filterExpression)
    {
        ICondition? filterCondition = ParseFilterExpression(filterExpression) ?? throw new InvalidExpressionException();
        List<long> ids = [];
        await ComicHandle.Enqueue(() =>
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

    void IPluginContext.RegisterPage(string host, Type pageType)
    {
        AppRouter.RegisterPage(host, pageType);
    }

    void IPluginContext.RegisterComicVirtualProperty(IVirtualProperty<IComicModel> property)
    {
        ArgumentNullException.ThrowIfNull(property, nameof(property));

        string name = property.Name;
        if (string.IsNullOrEmpty(name) || !VirtualPropertyNameRegex().IsMatch(name))
        {
            Logger.F(TAG, $"({Name}) RegisterComicVirtualProperty: Invalid name '{name}'");
            return;
        }

        if (!_comicVirtualProperties.TryAdd(name, property))
        {
            Logger.F(TAG, $"({Name}) RegisterComicVirtualProperty: Property '{name}' already registered");
        }
    }

    void IPluginContext.RegisterSidebarPage(ISidebarPageProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));
        _sidebarPageProviders.Add(provider);
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
            Logger.F(TAG, $"Unhandled exception thrown from plugin: {Name}", ex);
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
            Logger.F(TAG, $"Unhandled exception thrown from plugin: {Name}", ex);
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
