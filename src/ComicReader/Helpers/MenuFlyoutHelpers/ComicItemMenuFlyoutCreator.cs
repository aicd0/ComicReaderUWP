// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal static class ComicItemMenuFlyoutCreator
{
    public static List<BaseMenuFlyoutItemViewModel> CreateMenuItems(ComicItemViewModel model, IComicItemMenuFlyoutHandler handler)
    {
        List<BaseMenuFlyoutItemViewModel> result = [];
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.OpenInNewTab)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE8A5"
                },
                OnClick = handler.OnOpenInNewTabClicked,
            };

            result.Add(item);
        }

        if (!model.IsFavorite)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.AddToFavorites)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE734"
                },
                OnClick = handler.OnAddToFavoritesClicked,
            };

            result.Add(item);
        }

        if (model.IsFavorite)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.RemoveFromFavorites)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE8D9"
                },
                OnClick = handler.OnRemoveFromFavoritesClicked,
            };

            result.Add(item);
        }

        {
            MenuFlyoutSubItemViewModel groupItem = new(StringResourceProvider.Instance.SetCompletionState)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE7C1"
                },
            };

            if (model.Comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.NotStarted)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsUnread)
                {
                    OnClick = handler.OnMarkAsUnreadClicked,
                };
                groupItem.Items.Add(item);
            }

            if (model.Comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.Started)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsReading)
                {
                    OnClick = handler.OnMarkAsReadingClicked,
                };

                groupItem.Items.Add(item);
            }
            if (model.Comic.CompletionState != Data.Models.Comic.ComicCompletionStatusEnum.Completed)
            {
                MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.MarkAsRead)
                {
                    OnClick = handler.OnMarkAsReadClicked,
                };

                groupItem.Items.Add(item);
            }

            result.Add(groupItem);
        }

        if (!model.IsHide)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Hide)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uED1A"
                },
                OnClick = handler.OnHideClicked,
            };

            result.Add(item);
        }

        if (model.IsHide)
        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Unhide)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE7B3"
                },
                OnClick = handler.OnUnhideClicked,
            };

            result.Add(item);
        }

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Edit)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE70F"
                },
                OnClick = handler.OnEditClick,
            };

            result.Add(item);
        }

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.OpenInFileExplorer)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE838"
                },
                OnClick = handler.OnOpenInFileExplorerClicked,
            };

            result.Add(item);
        }

        result.Add(new MenuFlyoutSeperatorViewModel());

        {
            MenuFlyoutItemViewModel item = new(StringResourceProvider.Instance.Select)
            {
                Icon = new FontIcon
                {
                    Glyph = "\uE762"
                },
                OnClick = handler.OnSelectClicked,
            };

            result.Add(item);
        }

        return result;
    }
}
