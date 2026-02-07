// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.SidePane.Playlist;

internal sealed partial class PlaylistPage : BasePage
{
    private const string TAG = nameof(PlaylistPage);

    private readonly PlaylistPageViewModel ViewModel = new();

    public PlaylistPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        ViewModel.Initialize(PageActionHandler);
        ObserveData();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ViewModel.Destory();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, _ =>
        {
            ViewModel.UpdateComics();
        });

        GetEventBus().With<PlaybackModel>(EventId.PlaybackChanged).ObserveSticky(this, ViewModel.SetPlayback);

        ViewModel.ScrollToItemLiveData.ObserveSticky(this, MainListView.ScrollIntoView);
    }

    //
    // Events
    //

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelectedIndex(((ListView)sender).SelectedIndex);
    }

    private void ListView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.OriginalSource is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not PlaylistItemViewModel viewModel)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Start(async () =>
        {
            FlyoutBase? flyout = await viewModel.CreateContextFlyout();
            if (flyout is null)
            {
                return;
            }

            if (args.TryGetPosition(fe, out Windows.Foundation.Point point))
            {
                flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
            }
            else
            {
                flyout.ShowAt(fe);
            }
        });
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }
}
