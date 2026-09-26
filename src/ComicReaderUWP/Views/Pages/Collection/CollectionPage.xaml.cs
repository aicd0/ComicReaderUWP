// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
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
using Microsoft.UI.Xaml.Media.Animation;

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
    // Docked bar
    //

    private bool _isBarDocked = false;
    private double _dockOffset = 0.0;
    private Storyboard? _itemCountTextBlockAnimation = null;
    private Storyboard? _dockedBarBackgroundAnimation = null;

    private void ItemsView_ScrollOffsetChanged(double verticalOffset)
    {
        double barHeight = DockedBar.ActualHeight;
        double headerHeight = HeaderRoot.ActualHeight;
        if (barHeight <= 0.0 || headerHeight <= 0.0)
        {
            return;
        }

        if (_isBarDocked)
        {
            if (verticalOffset <= _dockOffset - 2.0)
            {
                DockedBarHost.Children.Remove(DockedBar);

                DockedBarSpacer.ClearValue(HeightProperty);
                DockedBarSpacer.Children.Add(DockedBar);

                AnimateOpacity(ItemCountTextBlock, 1.0, ref _itemCountTextBlockAnimation);
                AnimateOpacity(DockedBarBackground, 0.0, ref _dockedBarBackgroundAnimation);

                _isBarDocked = false;
            }

            return;
        }

        _dockOffset = HeaderRoot.Margin.Top + headerHeight - barHeight - HeaderRoot.Margin.Bottom;
        if (verticalOffset >= _dockOffset)
        {
            double left = DockedBar.TransformToVisual(ItemsView).TransformPoint(new(0.0, 0.0)).X;
            double right = ItemsView.ActualWidth - left - DockedBar.ActualWidth;

            DockedBarSpacer.Height = barHeight;
            DockedBarSpacer.Children.Remove(DockedBar);

            DockedBarHost.Margin = new Thickness(left, 0.0, right, 0.0);
            DockedBarHost.Children.Add(DockedBar);

            AnimateOpacity(ItemCountTextBlock, 0.0, ref _itemCountTextBlockAnimation);
            AnimateOpacity(DockedBarBackground, 1.0, ref _dockedBarBackgroundAnimation);

            _isBarDocked = true;
        }
    }

    private static void AnimateOpacity(UIElement target, double to, ref Storyboard? animation)
    {
        double from = target.Opacity;
        animation?.Stop();
        animation = null;

        var doubleAnimation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(Math.Abs(to - from) * 0.2)),
        };
        Storyboard.SetTarget(doubleAnimation, target);
        Storyboard.SetTargetProperty(doubleAnimation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(doubleAnimation);
        storyboard.Begin();
        animation = storyboard;
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }
}
