// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.Data.Models.TagInfo;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.Helpers.MenuFlyoutHelpers;

internal static class MenuFlyoutItemsCreator
{
    public const string CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU = "ComicItemMenu";
    public const string CUSTOM_ACTION_NAME_SELECT = "Select";

    public static async Task<List<BaseMenuFlyoutItemModel>> CreateComicMenuItems(
        ActionHandler actionHandler,
        ComicModel primaryComic,
        PlaylistModel.Builder? playlist = null,
        PlaybackModel.Builder? playback = null,
        IEnumerable<ComicModel>? selectedComics = null,
        bool canOpenWithDefault = false,
        bool canEdit = true,
        bool canSelect = false)
    {
        if (selectedComics is null)
        {
            selectedComics = [primaryComic];
        }
        else
        {
            if (selectedComics.Any(i => i.Id == primaryComic.Id))
            {
                playlist = new PlaylistModel.Builder().AddComics(selectedComics);
                canOpenWithDefault = true;
            }
            else
            {
                selectedComics = [primaryComic];
            }
        }

        List<ComicModel> inLibraryComics = [.. selectedComics.Where(x => !x.IsExternal)];

        Route primaryComicRoute = OpenComicHelper.GetComicRoute(
            primaryComic,
            playlist: playlist,
            playback: playback);

        List<BaseMenuFlyoutItemModel> items = [];

        if (canOpenWithDefault)
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Open,
                Icon = new FontIconSource() { Glyph = "\uE8B9" },
                Click = () =>
                {
                    OpenComicHelper.OpenComic(actionHandler, primaryComicRoute);
                },
            });
        }

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.OpenInNewTab,
            Icon = new FontIconSource() { Glyph = "\uE8A5" },
            Click = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, primaryComicRoute.Url)
                    .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                    .Build();
                actionHandler.HandleNoResult(actionModel);
            },
        });

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.SendToWindow,
            Icon = new FontIconSource() { Glyph = "\uE78B" },
            Items = CreateSendToWindowMenuItems(actionHandler, primaryComicRoute),
        });

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.ShowInFileExplorer,
            Icon = new FontIconSource() { Glyph = "\uE838" },
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    ErrorResult err = await primaryComic.ShowInFileExplorer();
                    err.DisplayErrorMessage(actionHandler);
                });
            },
        });

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Links,
            Icon = new FontIconSource() { Glyph = "\uE71B" },
            Items = await CreateComicLinkMenuItems(primaryComic, actionHandler),
        });

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Tags,
            Icon = new FontIconSource() { Glyph = "\uE8EC" },
            Items = CreateComicTagMenuItems(primaryComic, actionHandler),
        });

        if (canEdit && inLibraryComics.Count > 0)
        {
            items.Add(new SeparatorMenuFlyoutItemModel());

            bool isFavorite = FavoriteModel.Instance.FromId(primaryComic.Id) != null;
            if (isFavorite)
            {
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = StringResourceProvider.Instance.RemoveFromFavorites,
                    Icon = new FontIconSource() { Glyph = "\uE8D9" },
                    Click = () =>
                    {
                        FavoriteModel.Instance.BatchRemoveWithId(inLibraryComics.ConvertAll(x => x.Id));
                    },
                });
            }
            else
            {
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = StringResourceProvider.Instance.AddToFavorites,
                    Icon = new FontIconSource() { Glyph = "\uE734" },
                    Click = () =>
                    {
                        FavoriteModel.Instance.BatchAdd(inLibraryComics.ConvertAll(x => new FavoriteModel.FavoriteItem
                        {
                            Id = x.Id,
                            Title = x.Title,
                        }));
                    },
                });
            }

            items.Add(new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.SetCompletionState,
                Icon = new FontIconSource() { Glyph = "\uE7C1" },
                Items = CreateCompletionStatusMenuItems(inLibraryComics),
            });

            if (primaryComic.Hidden)
            {
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = StringResourceProvider.Instance.Unhide,
                    Icon = new FontIconSource() { Glyph = "\uE7B3" },
                    Click = () =>
                    {
                        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
                        {
                            await Task.WhenAll(inLibraryComics.Select(x => x.SetHidden(false)));
                        }));
                    },
                });
            }
            else
            {
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = StringResourceProvider.Instance.Hide,
                    Icon = new FontIconSource() { Glyph = "\uED1A" },
                    Click = () =>
                    {
                        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
                        {
                            await Task.WhenAll(inLibraryComics.Select(x => x.SetHidden(true)));
                        }));
                    },
                });
            }

            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Edit,
                Icon = new FontIconSource() { Glyph = "\uE70F" },
                Click = () =>
                {
                    string idList = string.Join(',', inLibraryComics.Select(x => x.Id.ToString()));
                    ActionModel actionModel = ActionModel.Builder.Create(EditComicProvider.NAME)
                        .AddParameter(EditComicProvider.PARAM_COMIC_ID, idList)
                        .Build();
                    actionHandler.HandleNoResult(actionModel);
                },
            });

            items.Add(new SeparatorMenuFlyoutItemModel());

            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Remove,
                Icon = new FontIconSource()
                {
                    Glyph = "\uE74D",
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 69, 0)),
                },
                Click = () =>
                {
                    string idList = string.Join(',', inLibraryComics.Select(x => x.Id.ToString()));
                    ActionModel actionModel = ActionModel.Builder.Create(RemoveComicProvider.NAME)
                        .AddParameter(RemoveComicProvider.PARAM_COMIC_ID, idList)
                        .Build();
                    actionHandler.HandleNoResult(actionModel);
                },
            });
        }

        {
            var windowContext = PluginWindowContext.From(actionHandler);
            var pluginItems = PluginManager.Instance.GetActivePlugins()
                .SelectMany(ctx => ctx.GetComicMenuItems(windowContext, primaryComic, selectedComics))
                .ToImmutableList();
            if (pluginItems.Count > 0)
            {
                items.Add(new SeparatorMenuFlyoutItemModel());
                items.AddRange(pluginItems);
            }
        }

        if (canSelect)
        {
            items.Add(new SeparatorMenuFlyoutItemModel());
            items.Add(CreateSelectMenuItem(actionHandler));
        }

        return items;
    }

    public static async Task<List<BaseMenuFlyoutItemModel>> CreateTagLinkMenuItems(string tagCategory, string tag, ActionHandler actionHandler)
    {
        Dictionary<string, TagLinkModel.LinkModel> linkMap = [];

        TagCategoryInfoModel? tagCategoryInfo = await TagCategoryInfoModel.Get(tagCategory);
        if (tagCategoryInfo is not null)
        {
            var linkModel = TagLinkModel.Parse(tagCategoryInfo.GetExt(TagCategoryInfoExt.LINKS));
            foreach (TagLinkModel.LinkModel item in linkModel.Links)
            {
                item.ReplaceTagVariables(tagCategory, tag);
                linkMap[item.Name] = item;
            }
        }

        TagInfoModel? tagInfo = await TagInfoModel.Get(tagCategory, tag);
        if (tagInfo is not null)
        {
            var linkModel = TagLinkModel.Parse(tagInfo.GetExt(TagInfoExt.LINKS));
            foreach (TagLinkModel.LinkModel item in linkModel.Links)
            {
                item.ReplaceTagVariables(tagCategory, tag);
                linkMap[item.Name] = item;
            }
        }

        List<TagLinkModel.LinkModel> links = [.. linkMap.Values];
        links.Sort((a, b) => a.Name.CompareTo(b.Name));
        return CreateLinkMenuItems(actionHandler, links);
    }

    public static BaseMenuFlyoutItemModel CreateSelectMenuItem(ActionHandler actionHandler)
    {
        return new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Select,
            Icon = new FontIconSource() { Glyph = "\uE762" },
            Click = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(CustomActionProvider.NAME)
                    .AddParameter(CustomActionProvider.PARAM_SOURCE, CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU)
                    .AddParameter(CustomActionProvider.PARAM_NAME, CUSTOM_ACTION_NAME_SELECT)
                    .Build();
                actionHandler.HandleNoResult(actionModel);
            },
        };
    }

    public static async Task<List<BaseMenuFlyoutItemModel>> CreateComicGroupMenuItems(
        ActionHandler actionHandler,
        IReadOnlyList<ComicModel> comics,
        Action expandAllHandler,
        Action collapseAllHandler,
        IList<BaseMenuFlyoutItemModel>? customItems = null)
    {
        List<BaseMenuFlyoutItemModel> result = [];

        ComicModel? randomComic = comics.Count > 0 ? comics[Random.Shared.Next(comics.Count)] : null;
        if (randomComic is not null)
        {
            PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComics(comics);
            result.Add(new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.RandomComic,
                Icon = new FontIconSource() { Glyph = "\uE8B1" },
                Items = await CreateComicMenuItems(
                    actionHandler,
                    randomComic,
                    playlist: playlist,
                    canOpenWithDefault: true),
            });
        }

        result.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.ExpandAll,
            Icon = new FontIconSource() { Glyph = "\uECCD" },
            Click = expandAllHandler,
        });

        result.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.CollapseAll,
            Icon = new FontIconSource() { Glyph = "\uF165" },
            Click = collapseAllHandler,
        });

        if (customItems is not null && customItems.Count > 0)
        {
            result.Add(new SeparatorMenuFlyoutItemModel());
            foreach (BaseMenuFlyoutItemModel item in customItems)
            {
                result.Add(item);
            }
        }

        result.Add(new SeparatorMenuFlyoutItemModel());
        result.Add(CreateSelectMenuItem(actionHandler));
        return result;
    }

    public static List<BaseMenuFlyoutItemModel> CreateCompletionStatusMenuItems(IEnumerable<ComicModel> selectedComics)
    {
        List<ComicModel> comics = [.. selectedComics];

        CompletionStatusEnum? oldStatus = null;
        foreach (ComicModel comic in comics)
        {
            if (!oldStatus.HasValue)
            {
                oldStatus = comic.CompletionStatus;
                continue;
            }

            if (oldStatus.Value != comic.CompletionStatus)
            {
                oldStatus = null;
                break;
            }
        }

        List<BaseMenuFlyoutItemModel> items = [];

        foreach (CompletionStatusEnum status in CompletionStatusService.AllStatus)
        {
            bool same = oldStatus == status;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = CompletionStatusService.EnumToString(status),
                IsChecked = same,
                Click = same ? null : () =>
                {
                    CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
                    {
                        await Task.WhenAll(selectedComics.Select(x => x.SetCompletionStatus(status)));
                    }));
                },
            });
        }

        return items;
    }

    private static List<BaseMenuFlyoutItemModel> CreateSendToWindowMenuItems(ActionHandler actionHandler, Route route)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        int currentWindowId = -1;
        if (actionHandler.TryGetComponent(out IMainWindowComponent? mainWindowCom))
        {
            currentWindowId = mainWindowCom.WindowId;
        }

        Dictionary<int, string> windowInfo = App.Instance.WindowManager.GetAllWindowInfo();
        foreach (KeyValuePair<int, string> pair in windowInfo)
        {
            int windowId = pair.Key;
            if (windowId == currentWindowId)
            {
                continue;
            }

            string title = pair.Value;
            string name = StringResourceProvider.Instance.WindowN.Replace("$n", windowId.ToString());
            if (!string.IsNullOrEmpty(title))
            {
                name += $" ({title})";
            }

            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = name,
                Click = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, windowId.ToString())
                        .Build();
                    actionHandler.HandleNoResult(actionModel);
                }
            });
        }

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.NewWindow,
            Click = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
                    .Build();
                actionHandler.HandleNoResult(actionModel);
            }
        });

        return items;
    }

    private static async Task<List<BaseMenuFlyoutItemModel>> CreateComicLinkMenuItems(ComicModel comic, ActionHandler actionHandler)
    {
        Dictionary<string, TagLinkModel.LinkModel> linkMap = [];

        foreach (KeyValuePair<string, ComicTagCategory> tagItem in comic.Tags)
        {
            string tagCategory = tagItem.Key;
            TagCategoryInfoModel? tagCategoryInfo = await TagCategoryInfoModel.Get(tagCategory);
            List<TagLinkModel.LinkModel> tagCategoryLinks = tagCategoryInfo is null ? [] :
                TagLinkModel.Parse(tagCategoryInfo.GetExt(TagCategoryInfoExt.LINKS)).Links;
            foreach (string tag in tagItem.Value.Tags)
            {
                foreach (TagLinkModel.LinkModel item in tagCategoryLinks)
                {
                    item.Name = $"{item.Name} ({tag})";
                    item.ReplaceTagVariables(tagCategory, tag);
                    linkMap[item.Name] = item;
                }

                TagInfoModel? tagInfo = await TagInfoModel.Get(tagCategory, tag);
                if (tagInfo is not null)
                {
                    var linkModel = TagLinkModel.Parse(tagInfo.GetExt(TagInfoExt.LINKS));
                    foreach (TagLinkModel.LinkModel item in linkModel.Links)
                    {
                        item.Name = $"{item.Name} ({tag})";
                        item.ReplaceTagVariables(tagCategory, tag);
                        linkMap[item.Name] = item;
                    }
                }
            }
        }

        {
            string? linkJson = comic.GetExt(ComicExt.LINKS);
            var linkModel = TagLinkModel.Parse(linkJson);
            foreach (TagLinkModel.LinkModel item in linkModel.Links)
            {
                linkMap[item.Name] = item;
            }
        }

        List<TagLinkModel.LinkModel> links = [.. linkMap.Values];
        links.Sort((a, b) => a.Name.CompareTo(b.Name));
        return CreateLinkMenuItems(actionHandler, links);
    }

    private static List<BaseMenuFlyoutItemModel> CreateLinkMenuItems(ActionHandler actionHandler, List<TagLinkModel.LinkModel> links)
    {
        List<BaseMenuFlyoutItemModel> items = [];
        if (links.Count > 0)
        {
            foreach (TagLinkModel.LinkModel link in links)
            {
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = link.Name,
                    Click = () =>
                    {
                        if (StringUtils.TryNormalizeWebUrl(link.Link, out Uri? uri))
                        {
                            CoroutineUtils.Run(async () => await Windows.System.Launcher.LaunchUriAsync(uri));
                        }
                        else
                        {
                            ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
                                .AddParameter(MessageDialogProvider.PARAM_TITLE, StringResourceProvider.Instance.LinkErrorTitle)
                                .AddParameter(MessageDialogProvider.PARAM_MESSAGE, StringResourceProvider.Instance.LinkErrorContent.Replace("$link", link.Link))
                                .Build();
                            actionHandler.HandleNoResult(actionModel);
                        }
                    }
                });
            }
        }
        else
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.None,
                IsEnabled = false,
            });
        }

        return items;
    }

    private static List<BaseMenuFlyoutItemModel> CreateComicTagMenuItems(ComicModel comic, ActionHandler actionHandler)
    {
        List<BaseMenuFlyoutItemModel> items = [];
        var tags = comic.Tags
            .SelectMany(item => item.Value.Tags.Select(tag => (Category: item.Key, Tag: tag)))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Tag)
            .ToList();
        if (tags.Count > 0)
        {
            foreach ((string category, string tag) in tags)
            {
                string name = $"{tag} ({category})";
                items.Add(new SimpleMenuFlyoutItemModel()
                {
                    Text = name,
                    Click = () =>
                    {
                        string expression = $"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(category)}\"=\"{ExpressionUtils.EscapeString(tag)}\"";
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                            .WithParam(RouterConstants.ARG_KEYWORD, $"exp:\"{ExpressionUtils.EscapeString(expression)}\"");
                        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                            .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                            .Build();
                        actionHandler.HandleNoResult(actionModel);
                    }
                });
            }
        }
        else
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.None,
                IsEnabled = false,
            });
        }

        return items;
    }
}
