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

    public static List<BaseMenuFlyoutItemViewModel> CreateMenuItems(
        ComicModel comic,
        ActionHandler actionHandler,
        IComicItemMenuFlyoutHandler handler,
        bool supportSelection = false)
    {
        List<BaseMenuFlyoutItemViewModel> result = [];

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.OpenInNewTab)
            {
                Glyph = "\uE8A5",
                OnClick = handler.OnOpenInNewTabClicked,
            };
            result.Add(item);
        }

        result.Add(new MenuFlyoutSeperatorViewModel());

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.OpenInFileExplorer)
            {
                Glyph = "\uE838",
                OnClick = () =>
                {
                    var er = EventRecorder.Create("OpenInFileExplorer#OnClicked");
                    comic.ShowInFileExplorer(er);
                    er.DisplayErrorMessage(actionHandler);
                },
            };
            result.Add(item);
        }

        result.Add(new MenuFlyoutSeperatorViewModel());

        bool isFavorite = FavoriteModel.Instance.FromId(comic.Id) != null;
        if (!isFavorite)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.AddToFavorites)
            {
                Glyph = "\uE734",
                OnClick = handler.OnAddToFavoritesClicked,
            };
            result.Add(item);
        }

        if (isFavorite)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.RemoveFromFavorites)
            {
                Glyph = "\uE8D9",
                OnClick = handler.OnRemoveFromFavoritesClicked,
            };
            result.Add(item);
        }

        {
            MenuFlyoutSubItemViewModel groupItem = new(StringResourceProvider.Instance.SetCompletionState)
            {
                Glyph = "\uE7C1",
            };

            {
                MenuFlyoutToggleItemViewModel item = new(StringResourceProvider.Instance.CompletionStatusUnread)
                {
                    IsChecked = comic.CompletionState == ComicCompletionStatusEnum.NotStarted,
                    OnClick = handler.OnMarkAsUnreadClicked,
                };
                groupItem.Items.Add(item);
            }

            {
                MenuFlyoutToggleItemViewModel item = new(StringResourceProvider.Instance.CompletionStatusReading)
                {
                    IsChecked = comic.CompletionState == ComicCompletionStatusEnum.Started,
                    OnClick = handler.OnMarkAsReadingClicked,
                };
                groupItem.Items.Add(item);
            }

            {
                MenuFlyoutToggleItemViewModel item = new(StringResourceProvider.Instance.CompletionStatusFinished)
                {
                    IsChecked = comic.CompletionState == ComicCompletionStatusEnum.Completed,
                    OnClick = handler.OnMarkAsReadClicked,
                };
                groupItem.Items.Add(item);
            }

            result.Add(groupItem);
        }

        if (!comic.Hidden)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Hide)
            {
                Glyph = "\uED1A",
                OnClick = handler.OnHideClicked,
            };
            result.Add(item);
        }

        if (comic.Hidden)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Unhide)
            {
                Glyph = "\uE7B3",
                OnClick = handler.OnUnhideClicked,
            };
            result.Add(item);
        }

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Edit)
            {
                Glyph = "\uE70F",
                OnClick = handler.OnEditClick,
            };
            result.Add(item);
        }

        if (supportSelection)
        {
            result.Add(new MenuFlyoutSeperatorViewModel());

            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Select)
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
                result.Add(item);
            }
        }

        return result;
    }

    public static async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagLinkMenuItems(string tagCategory, string tag, ActionHandler actionHandler)
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
