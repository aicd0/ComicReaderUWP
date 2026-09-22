// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.UserControls.Misc;

using Microsoft.UI.Xaml.Controls;

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
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }
}
