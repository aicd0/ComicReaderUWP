// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal abstract class SimpleComicItemMenuFlyoutHandler(ComicModel comic) : IComicItemMenuFlyoutHandler
{
    void IComicItemMenuFlyoutHandler.OnAddToFavoritesClicked()
    {
        FavoriteModel.Instance.Add(comic.Id, comic.Title, true);
    }

    void IComicItemMenuFlyoutHandler.OnHideClicked()
    {
        CoroutineUtils.Start(async () =>
        {
            await comic.SaveHiddenAsync(true);
        });
    }

    void IComicItemMenuFlyoutHandler.OnItemTapped()
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
        OpenInCurrentTab(route);
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadClicked()
    {
        CoroutineUtils.Start(async () =>
        {
            await comic.SetCompletionStateToCompleted();
        });
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsReadingClicked()
    {
        CoroutineUtils.Start(async () =>
        {
            await comic.SetCompletionStateToStarted();
        });
    }

    void IComicItemMenuFlyoutHandler.OnMarkAsUnreadClicked()
    {
        CoroutineUtils.Start(async () =>
        {
            await comic.SetCompletionStateToNotStarted();
        });
    }

    void IComicItemMenuFlyoutHandler.OnOpenInFileExplorerClicked()
    {
        comic.ShowInFileExplorer();
    }

    void IComicItemMenuFlyoutHandler.OnOpenInNewTabClicked()
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
        OpenInNewTab(route);
    }

    void IComicItemMenuFlyoutHandler.OnRemoveFromFavoritesClicked()
    {
        FavoriteModel.Instance.RemoveWithId(comic.Id, true);
    }

    void IComicItemMenuFlyoutHandler.OnUnhideClicked()
    {
        CoroutineUtils.Start(async () =>
        {
            await comic.SaveHiddenAsync(false);
        });
    }

    public virtual void OnSelectClicked() { }

    public abstract void OnEditClick();

    protected abstract void OpenInCurrentTab(Route route);

    protected abstract void OpenInNewTab(Route route);
}
