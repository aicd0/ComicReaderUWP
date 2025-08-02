// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal interface IComicItemMenuFlyoutHandler
{
    void OnItemTapped();

    void OnOpenInNewTabClicked();

    void OnAddToFavoritesClicked();

    void OnRemoveFromFavoritesClicked();

    void OnHideClicked();

    void OnUnhideClicked();

    void OnMarkAsReadClicked();

    void OnMarkAsReadingClicked();

    void OnMarkAsUnreadClicked();

    void OnEditClick();

    void OnSelectClicked();

    void OnOpenInFileExplorerClicked();
}
