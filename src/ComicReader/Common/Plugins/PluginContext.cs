// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common.Expression;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.SDK.Plugins;
using ComicReader.SDK.Plugins.Comic;
using ComicReader.SDK.Plugins.Common;
using ComicReader.SDK.Plugins.Menu;

namespace ComicReader.Common.Plugins;

internal class PluginContext(IPlugin plugin) : IPluginContext
{
    private const string TAG = nameof(PluginContext);

    public IPlugin Plugin => plugin;

    private readonly string _pluginName = plugin.Name;

    //
    // Comics API
    //

    public async Task<IComicModel?> GetComicById(long id)
    {
        return await ComicModel.FromId(id, "PluginGetComicById");
    }

    public async Task<IEnumerable<long>> SearchComics(string filterExpression)
    {
        ICondition? filterCondition = ParseFilterExpression(filterExpression) ?? throw new InvalidExpressionException();
        List<long> ids = [];
        await ComicData.Enqueue("SearchComics", delegate
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
    // Main Page More Menu Items
    //

    private readonly List<IMenuItem> _mainPageMoreMenuItems = [];

    public void RegisterMainPageMoreMenuItem(IMenuItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_mainPageMoreMenuItems)
        {
            _mainPageMoreMenuItems.Add(item);
        }
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetMainPageMoreMenuItems()
    {
        lock (_mainPageMoreMenuItems)
        {
            return [.. _mainPageMoreMenuItems.Select(CreateHostMenuFlyoutItem)];
        }
    }

    //
    // Comic Edited Handlers
    //

    private IComicEditedHandler? _comicEditedHandlers = null;

    public void RegisterComicEditedHandler(IComicEditedHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_comicEditedHandlers is not null)
        {
            throw new InvalidOperationException($"Already registered: '{_pluginName}'");
        }

        _comicEditedHandlers = handler;
    }

    public void DispatchComicEditedEvent(IComicModel comic)
    {
        _comicEditedHandlers?.ComicEdited(comic);
    }

    //
    // Helpers
    //

    private static BaseMenuFlyoutItemModel CreateHostMenuFlyoutItem(IMenuItem item)
    {
        return item switch
        {
            SimpleMenuItem simpleMenuItem => new SimpleMenuFlyoutItemModel(simpleMenuItem.Text)
            {
                Glyph = simpleMenuItem.Glyph,
                IsEnabled = simpleMenuItem.IsEnabled,
                Click = simpleMenuItem.Click,
            },
            SeparatorMenuItem => new SeparatorMenuFlyoutItemModel(),
            ToggleMenuItem toggleMenuItem => new ToggleMenuFlyoutItemModel(toggleMenuItem.Text)
            {
                IsChecked = toggleMenuItem.IsChecked,
                Click = toggleMenuItem.Click,
            },
            SubItemMenuItem subItemMenuItem => new SubItemMenuFlyoutItemModel(subItemMenuItem.Text)
            {
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
