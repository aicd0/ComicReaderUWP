// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.TagInfo;
using ComicReader.Helpers.Navigation;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal static class MenuFlyoutItemsCreator
{
    public const string CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU = "ComicItemMenu";
    public const string CUSTOM_ACTION_NAME_SELECT = "Select";

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateMenuItems(
        ComicModel primaryComic,
        ActionHandler actionHandler,
        IEnumerable<ComicModel>? selectedComics = null,
        bool supportSelection = false)
    {
        selectedComics ??= [primaryComic];

        List<BaseMenuFlyoutItemViewModel> result = [];

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.OpenInNewTab)
        {
            Glyph = "\uE8A5",
            OnClick = () =>
            {
                Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                    .WithParam(RouterConstants.ARG_COMIC_ID, primaryComic.Id.ToString());
                ActionModel actionModel = ActionModel.Builder.Create(OpenInNewTabProvider.NAME)
                    .AddParameter(OpenInNewTabProvider.PARAM_URL, route.Url)
                    .Build();
                actionHandler.Handle(actionModel);
            },
        });

        result.Add(new MenuFlyoutSeperatorViewModel());

        result.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await CreateComicLinkMenuItems(primaryComic, actionHandler),
        });

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
                    .AddParameter(EditComicProvider.PARAM_ID, idList)
                    .Build();
                actionHandler.Handle(actionModel);
            },
        });

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

        if (supportSelection)
        {
            result.Add(new MenuFlyoutSeperatorViewModel());

            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Select)
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
            });
        }

        return result;
    }

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagLinkMenuItems(string tagCategory, string tag, ActionHandler actionHandler)
    {
        List<TagLinkModel.LinkModel> links = await GetTagLinks(tagCategory, tag);
        links.Sort((a, b) => a.Name.CompareTo(b.Name));
        return CreateLinkMenuItems(actionHandler, links);
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
                TagInfoModel? tagModel = await TagInfoModel.Get(tagData.Name, tag);
                if (tagModel is null)
                {
                    continue;
                }

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
}
