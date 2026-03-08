// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.AppEnvironment;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.SidePane.History;

internal sealed partial class HistoryPage : BasePage
{
    public ObservableCollection<HistoryGroupViewModel> DataSource { get; set; } = [];

    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        CoroutineUtils.Run(Update);
        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.HistoryUpdated.Observe(this, delegate
        {
            CoroutineUtils.Run(Update);
        });
    }

    private async Task Update()
    {
        var source = new List<HistoryGroupViewModel>();
        HistoryGroupViewModel? currentGroup = null;
        List<ComicHistoryItemModel> historyItems = await ComicHistoryItemModel.GetAllAsync();
        historyItems.Sort((x, y) => y.DateTime.CompareTo(x.DateTime));
        foreach (ComicHistoryItemModel item in historyItems)
        {
            DateTimeOffset localTime = item.DateTime.ToLocalTime();
            string key = localTime.ToString("D", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo());
            if (currentGroup != null && !currentGroup.Key.Equals(key))
            {
                source.Add(currentGroup);
                currentGroup = null;
            }

            currentGroup ??= new HistoryGroupViewModel(key);
            var itemOut = new HistoryItemViewModel
            {
                Id = item.ComicId,
                Time = localTime.ToString("t", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo()),
                Title = item.Title
            };

            currentGroup.Add(itemOut);
        }

        if (currentGroup != null)
        {
            source.Add(currentGroup);
        }

        DataSource.Clear();
        foreach (HistoryGroupViewModel item in source)
        {
            DataSource.Add(item);
        }

        MainListView.SelectedIndex = -1;
        TbNoHistory.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task OpenItem(HistoryItemViewModel item, bool newTab)
    {
        ComicModel? comic = await ComicModel.FromId(item.Id, "HistoryLoadComic");
        if (comic is null)
        {
            DeleteItem(item);
            return;
        }

        IEnumerable<long> playlistComicIds = DataSource.SelectMany(x => x).Select(x => x.Id);
        PlaylistModel.Builder playlist = PlaylistModel.Builder.Create().AddComicIds(playlistComicIds);
        if (newTab)
        {
            Route route = OpenComicHelper.GetComicRoute(comic, playlist);
            GetMainPageAbility().OpenInNewTab(route);
        }
        else
        {
            OpenComicHelper.OpenComic(PageActionHandler, OpenComicHelper.GetComicRoute(comic, playlist));
        }

        GetMainPageAbility().SetSidePaneOpenState(false, force: false);
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private void DeleteItem(HistoryItemViewModel item)
    {
        CoroutineUtils.Run(() => ComicHistoryItemModel.RemoveAsync(item.Id, suppressEvent: true));
        var source = (ObservableCollection<HistoryGroupViewModel>)HistorySource.Source;

        for (int i = 0; i < source.Count; ++i)
        {
            HistoryGroupViewModel group = source[i];

            for (int j = 0; j < group.Count; ++j)
            {
                HistoryItemViewModel item2 = group[j];

                if (item2.Id == item.Id)
                {
                    group.RemoveAt(j);
                    --j;
                }
            }

            if (group.Count == 0)
            {
                source.RemoveAt(i);
                --i;
            }
        }

        TbNoHistory.Visibility = source.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // events
    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
            await OpenItem(item, true);
        });
    }

    private void OnDeleteItemClicked(object sender, RoutedEventArgs e)
    {
        var item = (HistoryItemViewModel)((MenuFlyoutItem)sender).DataContext;
        DeleteItem(item);
    }

    private void MainListViewItemClick(object sender, ItemClickEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var item = (HistoryItemViewModel)e.ClickedItem;
            await OpenItem(item, false);
        });
    }
}
