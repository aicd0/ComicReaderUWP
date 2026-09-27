// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.UserControls.Misc;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace ComicReaderUWP.Views.Pages.Search;

internal sealed partial class SearchPage : BasePage
{
    private SearchPageViewModel ViewModel { get; set; } = new SearchPageViewModel();

    private readonly SearchNavigationBar _searchNavigationBar;
    private string _keyword = "";

    public SearchPage()
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

        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");

        string searchText = _keyword.Trim();
        string titleText;
        string tabTitle;
        if (searchText.Length > 0)
        {
            titleText = $"\"{searchText}\"";
            tabTitle = StringResourceProvider.Instance.SearchResultsOf.Replace("$keyword", searchText);
        }
        else
        {
            titleText = StringResourceProvider.Instance.AllMatchedResults;
            tabTitle = StringResourceProvider.Instance.SearchResults;
        }

        GetMainPageAbility().SetTitle(tabTitle);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Find });
        GetMainPageAbility().SetCustomNavigationBar(_searchNavigationBar);

        ViewModel.Initialize(PageActionHandler, _keyword);
        ViewModel.Title = titleText;
        ViewModel.NoResultText = StringResourceProvider.Instance.NoResults.Replace("$keyword", searchText);

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        _searchNavigationBar.SetSearchBox(_keyword);
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh();
        });

        ViewModel.ResultsLiveData.ObserveSticky(this, results =>
        {
            ItemsView.SetItems(results);
        });

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
    // Docked bar
    //

    private bool _isBarDocked = false;
    private double _dockOffset = 0.0;
    private Storyboard? _titleTextBlockAnimation = null;
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

                AnimateOpacity(TitleTextBlock, 1.0, ref _titleTextBlockAnimation);
                AnimateOpacity(DockedBarBackground, 0.0, ref _dockedBarBackgroundAnimation);

                _isBarDocked = false;
            }

            return;
        }

        _dockOffset = ItemsView.ContentPadding.Top + HeaderRoot.Margin.Top + headerHeight - barHeight - DockedBarSpacer.Margin.Bottom;
        if (verticalOffset >= _dockOffset)
        {
            double left = DockedBar.TransformToVisual(ItemsView).TransformPoint(new(0.0, 0.0)).X;
            double right = ItemsView.ActualWidth - left - DockedBar.ActualWidth;

            DockedBarSpacer.Height = barHeight;
            DockedBarSpacer.Children.Remove(DockedBar);

            DockedBarHost.Margin = new Thickness(left, 0.0, right, 0.0);
            DockedBarHost.Children.Add(DockedBar);

            AnimateOpacity(TitleTextBlock, 0.0, ref _titleTextBlockAnimation);
            AnimateOpacity(DockedBarBackground, 1.0, ref _dockedBarBackgroundAnimation);

            _isBarDocked = true;
        }
    }

    private static void AnimateOpacity(UIElement target, double to, ref Storyboard? animation)
    {
        double from = target.Opacity;
        if (animation is not null)
        {
            animation.Stop();
            animation = null;
        }

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
