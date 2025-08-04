// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.ViewModels;

namespace ComicReader.UserControls.ComicItemView;

internal class BaseComicItemMenuFlyoutHandler(ComicItemViewModel item, IComicItemViewHandler handler) : IComicItemMenuFlyoutHandler
{
    void IComicItemMenuFlyoutHandler.OnAddToFavoritesClicked()
    {
        handler.OnAddToFavoritesClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnEditClick()
    {
        handler.OnEditClick(item);
    }

    void IComicItemMenuFlyoutHandler.OnHideClicked()
    {
        handler.OnHideClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadClicked()
    {
        handler.OnMarkAsReadClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadingClicked()
    {
        handler.OnMarkAsReadingClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsUnreadClicked()
    {
        handler.OnMarkAsUnreadClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnOpenInNewTabClicked()
    {
        handler.OnOpenInNewTabClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnRemoveFromFavoritesClicked()
    {
        handler.OnRemoveFromFavoritesClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnSelectClicked()
    {
        handler.OnSelectClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnUnhideClicked()
    {
        handler.OnUnhideClicked(item);
    }

    void IComicItemMenuFlyoutHandler.OnOpenInFileExplorerClicked()
    {
        item.Comic.ShowInFileExplorer();
    }
}
