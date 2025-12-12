// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Components;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.Expression;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.TagInfo;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal static class MenuFlyoutItemsCreator
{
    public const string CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU = "ComicItemMenu";
    public const string CUSTOM_ACTION_NAME_SELECT = "Select";

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateMenuItems(
        ComicModel primaryComic,
        ActionHandler actionHandler,
        IEnumerable<ComicModel>? selectedComics = null,
        bool canEdit = true,
        bool canSelect = false)
    {
        // If primaryComic is not in selectedComics, ignore selectedComics and use only primaryComic.
        selectedComics ??= [primaryComic];
        if (!selectedComics.Any(i => i.Id == primaryComic.Id))
        {
            selectedComics = [primaryComic];
        }

        var primaryComicRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER);
        if (primaryComic.IsExternal)
        {
            primaryComicRoute.WithParam(RouterConstants.ARG_COMIC_LOCATION, primaryComic.Location);
        }
        else
        {
            primaryComicRoute.WithParam(RouterConstants.ARG_COMIC_ID, primaryComic.Id.ToString());
        }

        List<BaseMenuFlyoutItemViewModel> result = [];

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.OpenInNewTab)
        {
            Glyph = "\uE8A5",
            OnClick = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, primaryComicRoute.Url)
                    .Build();
                actionHandler.Handle(actionModel);
            },
        });

        result.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.SendToWindow)
        {
            Glyph = "\uE78B",
            Items = CreateSendToWindowMenuItems(primaryComicRoute.Url, actionHandler),
        });

        result.Add(new MenuFlyoutSeperatorViewModel());

        result.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await CreateComicLinkMenuItems(primaryComic, actionHandler),
        });

        result.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Tags)
        {
            Glyph = "\uE8EC",
            Items = CreateComicTagMenuItems(primaryComic, actionHandler),
        });

        if (canEdit && !selectedComics.All(i => i.IsExternal))
        {
            result.Add(new MenuFlyoutSeperatorViewModel());

            bool isFavorite = FavoriteModel.Instance.FromId(primaryComic.Id) != null;
            if (isFavorite)
            {
                result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.RemoveFromFavorites)
                {
                    Glyph = "\uE8D9",
                    OnClick = () =>
                    {
                        List<ComicModel> items = [.. selectedComics];
                        FavoriteModel.Instance.BatchRemoveWithId(items.ConvertAll(x => x.Id));
                    },
                });
            }
            else
            {
                result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.AddToFavorites)
                {
                    Glyph = "\uE734",
                    OnClick = () =>
                    {
                        List<ComicModel> items = [.. selectedComics];
                        FavoriteModel.Instance.BatchAdd(items.ConvertAll(x => new FavoriteModel.FavoriteItem
                        {
                            Id = x.Id,
                            Title = x.Title,
                        }));
                    },
                });
            }

            {
                MenuFlyoutSubItemViewModel groupItem = new(StringResourceProvider.Instance.SetCompletionState)
                {
                    Glyph = "\uE7C1",
                };

                groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusUnread)
                {
                    IsChecked = primaryComic.CompletionState == ComicCompletionStatusEnum.NotStarted,
                    OnClick = async () =>
                    {
                        foreach (ComicModel comic in selectedComics)
                        {
                            await comic.SetCompletionStateToNotStarted();
                        }
                    },
                });

                groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusReading)
                {
                    IsChecked = primaryComic.CompletionState == ComicCompletionStatusEnum.Started,
                    OnClick = async () =>
                    {
                        foreach (ComicModel comic in selectedComics)
                        {
                            await comic.SetCompletionStateToStarted();
                        }
                    },
                });

                groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusFinished)
                {
                    IsChecked = primaryComic.CompletionState == ComicCompletionStatusEnum.Completed,
                    OnClick = async () =>
                    {
                        foreach (ComicModel comic in selectedComics)
                        {
                            await comic.SetCompletionStateToCompleted();
                        }
                    },
                });

                result.Add(groupItem);
            }

            if (primaryComic.Hidden)
            {
                result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Unhide)
                {
                    Glyph = "\uE7B3",
                    OnClick = async () =>
                    {
                        foreach (ComicModel comic in selectedComics)
                        {
                            await comic.SaveHiddenAsync(false);
                        }
                    },
                });
            }
            else
            {
                result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Hide)
                {
                    Glyph = "\uED1A",
                    OnClick = async () =>
                    {
                        foreach (ComicModel comic in selectedComics)
                        {
                            await comic.SaveHiddenAsync(true);
                        }
                    },
                });
            }

            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
            {
                Glyph = "\uE70F",
                OnClick = () =>
                {
                    List<ComicModel> items = [.. selectedComics];
                    string idList = string.Join(',', items.ConvertAll(x => x.Id.ToString()));
                    ActionModel actionModel = ActionModel.Builder.Create(EditComicProvider.NAME)
                        .AddParameter(EditComicProvider.PARAM_COMIC_ID, idList)
                        .Build();
                    actionHandler.Handle(actionModel);
                },
            });
        }

        result.Add(new MenuFlyoutSeperatorViewModel());

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.OpenInFileExplorer)
        {
            Glyph = "\uE838",
            OnClick = () =>
            {
                var er = EventRecorder.Create("OpenInFileExplorer#OnClicked");
                primaryComic.ShowInFileExplorer(er);
                er.DisplayErrorMessage(actionHandler);
            },
        });

        if (canSelect)
        {
            result.Add(new MenuFlyoutSeperatorViewModel());
            result.Add(CreateSelectMenuItem(actionHandler));
        }

        return result;
    }

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagLinkMenuItems(string tagCategory, string tag, ActionHandler actionHandler)
    {
        List<TagLinkModel.LinkModel> links = await GetTagLinks(tagCategory, tag);
        links.Sort((a, b) => a.Name.CompareTo(b.Name));
        return CreateLinkMenuItems(actionHandler, links);
    }

    public static BaseMenuFlyoutItemViewModel CreateSelectMenuItem(ActionHandler actionHandler)
    {
        return new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Select)
        {
            Glyph = "\uE762",
            OnClick = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(CustomActionProvider.NAME)
                    .AddParameter(CustomActionProvider.PARAM_SOURCE, CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU)
                    .AddParameter(CustomActionProvider.PARAM_NAME, CUSTOM_ACTION_NAME_SELECT)
                    .Build();
                actionHandler.Handle(actionModel);
            },
        };
    }

    private static List<BaseMenuFlyoutItemViewModel> CreateSendToWindowMenuItems(string url, ActionHandler actionHandler)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        int currentWindowId = -1;
        if (actionHandler.TryGetComponent<IMainWindowComponent>(out IMainWindowComponent? mainWindowCom))
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

            items.Add(new MenuFlyoutItemViewModel(name)
            {
                OnClick = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, url)
                        .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, windowId.ToString())
                        .AddParameter(OpenTabProvider.PARAM_NEW_TAB, "0")
                        .Build();
                    actionHandler.Handle(actionModel);
                }
            });
        }

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.NewWindow)
        {
            OnClick = () =>
            {
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, url)
                    .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
                    .Build();
                actionHandler.Handle(actionModel);
            }
        });

        return items;
    }

    private static async Task<List<BaseMenuFlyoutItemViewModel>> CreateComicLinkMenuItems(ComicModel comic, ActionHandler actionHandler)
    {
        List<TagLinkModel.LinkModel> links = [];

        {
            string? linkJson = comic.GetExt(ComicExt.LINKS);
            var linkModel = TagLinkModel.Parse(linkJson);
            linkModel.Links.Sort((a, b) => a.Name.CompareTo(b.Name));
            links.AddRange(linkModel.Links);
        }

        foreach (ComicData.TagData tagData in comic.Tags)
        {
            foreach (string tag in tagData.Tags)
            {
                List<TagLinkModel.LinkModel> tagLinks = await GetTagLinks(tagData.Name, tag);
                foreach (TagLinkModel.LinkModel link in tagLinks)
                {
                    link.Name = $"{link.Name} ({tag})";
                }

                tagLinks.Sort((a, b) => a.Name.CompareTo(b.Name));
                links.AddRange(tagLinks);
            }
        }

        return CreateLinkMenuItems(actionHandler, links);
    }

    private static async Task<List<TagLinkModel.LinkModel>> GetTagLinks(string tagCategory, string tag)
    {
        TagCategoryInfoModel? tagCategoryInfo = await TagCategoryInfoModel.Get(tagCategory);
        TagInfoModel? tagInfo = await TagInfoModel.Get(tagCategory, tag);
        List<TagLinkModel.LinkModel> links = [];

        if (tagCategoryInfo != null)
        {
            var linkModel = TagLinkModel.Parse(tagCategoryInfo.GetExt(TagCategoryInfoExt.LINKS));
            links.AddRange(linkModel.Links);
        }

        if (tagInfo != null)
        {
            var linkModel = TagLinkModel.Parse(tagInfo.GetExt(TagInfoExt.LINKS));
            links.AddRange(linkModel.Links);
        }

        foreach (TagLinkModel.LinkModel link in links)
        {
            string tagEscaped = Uri.EscapeDataString(tag);
            string tagCategoryEscaped = Uri.EscapeDataString(tagCategory);
            link.Link = link.Link
                .Replace("{%tag}", tag)
                .Replace("{%tag_category}", tagCategory)
                .Replace("{%tag_escaped}", tagEscaped)
                .Replace("{%tag_category_escaped}", tagCategoryEscaped);
        }

        return links;
    }

    private static List<BaseMenuFlyoutItemViewModel> CreateLinkMenuItems(ActionHandler actionHandler, List<TagLinkModel.LinkModel> links)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];
        if (links.Count > 0)
        {
            foreach (TagLinkModel.LinkModel link in links)
            {
                items.Add(new MenuFlyoutItemViewModel(link.Name)
                {
                    OnClick = () =>
                    {
                        if (StringUtils.TryNormalizeWebUrl(link.Link, out Uri? uri))
                        {
                            _ = Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                        else
                        {
                            ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
                                .AddParameter(MessageDialogProvider.PARAM_TITLE, StringResourceProvider.Instance.LinkErrorTitle)
                                .AddParameter(MessageDialogProvider.PARAM_MESSAGE, StringResourceProvider.Instance.LinkErrorContent.Replace("$link", link.Link))
                                .Build();
                            actionHandler.Handle(actionModel);
                        }
                    }
                });
            }
        }
        else
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.None)
            {
                IsEnabled = false,
            });
        }

        return items;
    }

    private static List<BaseMenuFlyoutItemViewModel> CreateComicTagMenuItems(ComicModel comic, ActionHandler actionHandler)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];
        var tags = comic.Tags
            .SelectMany(tagData => tagData.Tags.Select(tag => (Category: tagData.Name, Tag: tag)))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Tag)
            .ToList();
        if (tags.Count > 0)
        {
            foreach ((string Category, string Tag) pair in tags)
            {
                string name = $"{pair.Tag} ({pair.Category})";
                items.Add(new MenuFlyoutItemViewModel(name)
                {
                    OnClick = () =>
                    {
                        string expression = $"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(pair.Category)}\"=\"{ExpressionUtils.EscapeString(pair.Tag)}\"";
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                            .WithParam(RouterConstants.ARG_KEYWORD, $"exp:\"{ExpressionUtils.EscapeString(expression)}\"");
                        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                            .Build();
                        actionHandler.Handle(actionModel);
                    }
                });
            }
        }
        else
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.None)
            {
                IsEnabled = false,
            });
        }

        return items;
    }
}
