// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;

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

            if (comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.NotStarted)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsUnread)
                {
                    OnClick = handler.OnMarkAsUnreadClicked,
                };
                groupItem.Items.Add(item);
            }

            if (comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.Started)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsReading)
                {
                    OnClick = handler.OnMarkAsReadingClicked,
                };

                groupItem.Items.Add(item);
            }

            if (comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.Completed)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsRead)
                {
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
}
