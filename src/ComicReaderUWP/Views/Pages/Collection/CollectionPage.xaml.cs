// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.UserControls.Misc;
using ComicReaderUWP.Views.Dialogs.EditComicInfo;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.Views.Pages.Collection;

internal sealed partial class CollectionPage : BasePage
{
    private readonly CollectionPageViewModel ViewModel = new();
    private readonly SearchNavigationBar _searchNavigationBar;

    public CollectionPage()
    {
        InitializeComponent();
        _searchNavigationBar = new();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ItemsView.Initialize(PageActionHandler);
        ViewTypeSelector.ViewTypeChanged += ViewModel.SelectViewType;

        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.Collection);
        GetMainPageAbility().SetIcon(new FontIconSource() { Glyph = "\uF5ED" });
        GetMainPageAbility().SetCustomNavigationBar(_searchNavigationBar);

        long collectionId = -1;
        if (long.TryParse(bundle.GetString(RouterConstants.ARG_ID), out long parsedId))
        {
            collectionId = parsedId;
        }

        ViewModel.Initialize(PageActionHandler, collectionId);

        ObserveData();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh();
        });

        GlobalEvent.Instance.CollectionUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh();
        });

        ViewModel.ResultsLiveData.ObserveSticky(this, items =>
        {
            ItemsView.SetItems(items);
        });

        ViewModel.TitleLiveData.ObserveSticky(this, title =>
        {
            GetMainPageAbility().SetTitle(title);
        });

        _searchNavigationBar.SearchTextChange += ViewModel.SetSearchText;

        _searchNavigationBar.SearchTextSubmitted += text =>
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, text);
            GetMainPageAbility().OpenInCurrentTab(route);
        };
    }

    //
    // Header
    //

    private void RatingControl_ValueChanged(RatingControl sender, object args)
    {
        ViewModel.SetRating(sender.Value);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        ComicModel? collection = ViewModel.Collection;
        if (collection is null)
        {
            return;
        }

        var dialog = new EditComicInfoDialog([collection]);
        CoroutineUtils.Run(() => dialog.ShowAsync(WindowId));
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleFavorite();
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            ComicModel? collection = ViewModel.Collection;
            if (collection is null || sender is not FrameworkElement anchor)
            {
                return;
            }

            List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicMenuItems(
                PageActionHandler,
                collection);
            if (menuItems.Count == 0)
            {
                return;
            }

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            flyout.ShowAt(anchor, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        });
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }
}
