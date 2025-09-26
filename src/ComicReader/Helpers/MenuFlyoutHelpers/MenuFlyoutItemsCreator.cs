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

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal static class MenuFlyoutItemsCreator
{
    public const string CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU = "ComicItemMenu";
    public const string CUSTOM_ACTION_NAME_SELECT = "Select";

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateMenuItems(
        ComicModel comic,
        ActionHandler actionHandler,
        IComicItemMenuFlyoutHandler handler,
        bool supportSelection = false)
    {
        List<BaseMenuFlyoutItemViewModel> result = [];

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.OpenInNewTab)
        {
            Glyph = "\uE8A5",
            OnClick = handler.OnOpenInNewTabClicked,
        });

        result.Add(new MenuFlyoutSeperatorViewModel());

        result.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await CreateComicLinkMenuItems(comic, actionHandler),
        });

        result.Add(new MenuFlyoutSeperatorViewModel());

        bool isFavorite = FavoriteModel.Instance.FromId(comic.Id) != null;
        if (isFavorite)
        {
            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.RemoveFromFavorites)
            {
                Glyph = "\uE8D9",
                OnClick = handler.OnRemoveFromFavoritesClicked,
            });
        }
        else
        {
            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.AddToFavorites)
            {
                Glyph = "\uE734",
                OnClick = handler.OnAddToFavoritesClicked,
            });
        }

        {
            MenuFlyoutSubItemViewModel groupItem = new(StringResourceProvider.Instance.SetCompletionState)
            {
                Glyph = "\uE7C1",
            };

            groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusUnread)
            {
                IsChecked = comic.CompletionState == ComicCompletionStatusEnum.NotStarted,
                OnClick = handler.OnMarkAsUnreadClicked,
            });

            groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusReading)
            {
                IsChecked = comic.CompletionState == ComicCompletionStatusEnum.Started,
                OnClick = handler.OnMarkAsReadingClicked,
            });

            groupItem.Items.Add(new MenuFlyoutToggleItemViewModel(StringResourceProvider.Instance.CompletionStatusFinished)
            {
                IsChecked = comic.CompletionState == ComicCompletionStatusEnum.Completed,
                OnClick = handler.OnMarkAsReadClicked,
            });

            result.Add(groupItem);
        }

        if (comic.Hidden)
        {
            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Unhide)
            {
                Glyph = "\uE7B3",
                OnClick = handler.OnUnhideClicked,
            });
        }
        else
        {
            result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Hide)
            {
                Glyph = "\uED1A",
                OnClick = handler.OnHideClicked,
            });
        }

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            OnClick = handler.OnEditClick,
        });

        result.Add(new MenuFlyoutSeperatorViewModel());

        result.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.OpenInFileExplorer)
        {
            Glyph = "\uE838",
            OnClick = () =>
            {
                var er = EventRecorder.Create("OpenInFileExplorer#OnClicked");
                comic.ShowInFileExplorer(er);
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
        List<BaseMenuFlyoutItemViewModel> items = await CreateTagLinkMenuItemsInternal(tagCategory, tag, actionHandler, fromComic: false);
        if (items.Count == 0)
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.None)
            {
                IsEnabled = false,
            });
        }

        return items;
    }

    private static async Task<List<BaseMenuFlyoutItemViewModel>> CreateComicLinkMenuItems(ComicModel comic, ActionHandler actionHandler)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];
        foreach (ComicData.TagData tagData in comic.Tags)
        {
            foreach (string tag in tagData.Tags)
            {
                TagInfoModel? tagModel = await TagInfoModel.Get(tagData.Name, tag);
                if (tagModel is null)
                {
                    continue;
                }

                List<BaseMenuFlyoutItemViewModel> tagLinkItems = await CreateTagLinkMenuItemsInternal(tagData.Name, tag, actionHandler, fromComic: true);
                items.AddRange(tagLinkItems);
            }
        }

        if (items.Count == 0)
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.None)
            {
                IsEnabled = false,
            });
        }

        return items;
    }

    private static async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagLinkMenuItemsInternal(string tagCategory, string tag, ActionHandler actionHandler, bool fromComic)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

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

        if (links.Count > 0)
        {
            links.Sort((a, b) => a.Name.CompareTo(b.Name));

            foreach (TagLinkModel.LinkModel link in links)
            {
                string name = link.Name;
                if (fromComic)
                {
                    name = $"{name} ({tag})";
                }

                items.Add(new MenuFlyoutItemViewModel(name)
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

        return items;
    }
}
