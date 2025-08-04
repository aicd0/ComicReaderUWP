// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

namespace ComicReader.UserControls.ComicItemView;

internal interface IComicItemViewHandler
{
    void OnItemTapped(ComicItemViewModel item);

    void OnOpenInNewTabClicked(ComicItemViewModel item);

    void OnAddToFavoritesClicked(ComicItemViewModel item);

    void OnRemoveFromFavoritesClicked(ComicItemViewModel item);

    void OnHideClicked(ComicItemViewModel item);

    void OnUnhideClicked(ComicItemViewModel item);

    void OnMarkAsReadClicked(ComicItemViewModel item);

    void OnMarkAsReadingClicked(ComicItemViewModel item);

    void OnMarkAsUnreadClicked(ComicItemViewModel item);

    void OnEditClick(ComicItemViewModel item);

    void OnSelectClicked(ComicItemViewModel item);
}
