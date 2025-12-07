// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.AppWindows.Main;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Views.Pages.Navigation;

internal sealed partial class NavigationPage : BasePage
{
    private const string TAG = nameof(NavigationPage);

    private bool _isFavorite = false;
    private bool _immersiveMode = false;
    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;
    private NavigationBundle? _pendingBundle;
    private NavigationBundle? _currentBundle;
    private readonly NavigationPageAbility _ability;
    private readonly LifecycleAwareAbility _lifecycleAbility = new();
    private readonly LifecycleAwareAbility _sidePaneLifecycleAbility = new();

    private NavigationPageViewModel ViewModel { get; } = new();

    public NavigationPage()
    {
        InitializeComponent();

        _ability = new(this);
        Background = AppearanceManager.Instance.GetThemeBackground();
        RightSidePane.Initialize(new SidePaneHandler(this));
        SyncSidebarOpenState(NavigationPageSidePane.IsPaneOpen);
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ObserveData();

        NavigationBundle? pendingBundle = _pendingBundle;
        if (pendingBundle is not null)
        {
            _pendingBundle = null;
            Navigate(pendingBundle);
        }
    }

    protected override void OnResume()
    {
        base.OnResume();

        UpdateUI();
    }

    private void ObserveData()
    {
        ComicData.IsScanningLibrary.ObserveSticky(this, scanning =>
        {
            ViewModel.Refreshing = scanning;
        });

        GetEventBus().With<double>(EventId.RootTabHeightChange).ObserveSticky(this, delegate (double h)
        {
            _rootTabHeight = h;
            UpdateTopPadding();
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            TopTile.Opacity = opacity;
            NavigationPageSidePane.Opacity = opacity;
        });

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, isFullscreen =>
        {
            ViewModel.IsFullscreen = isFullscreen;
        });

        ViewModel.OpenInNewWindowLiveData.Observe(this, route =>
        {
            MainWindow.Open(route.Url);
        });

        ViewModel.OpenInNewTabLiveData.Observe(this, route =>
        {
            GetMainPageAbility().OpenInNewTab(route);
        });

        ViewModel.FullscreenLiveData.Observe(this, isFullscreen =>
        {
            if (isFullscreen)
            {
                GetMainWindowAbility().EnterFullscreen();
            }
            else
            {
                GetMainWindowAbility().ExitFullscreen();
            }
        });

        _lifecycleAbility.Observe(this);
        _sidePaneLifecycleAbility.Observe(this);
    }

    private void UpdateUI()
    {
        MainReaderSettingPanel.SetWindowId(WindowId);
        ViewModel.UpdateMoreMenuItems();
        NavigationPageSidePane.IsPaneOpen = KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SIDE_PANE_OPENED, false);
        NavigationPageSidePane.OpenPaneLength = KVDatabase.Default.GetDouble(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SIDE_PANE_WIDTH, 380);
        RightSidePane.RestoreLastStatus();
    }

    //
    // Public API
    //

    public void Navigate(NavigationBundle bundle)
    {
        if (!Started)
        {
            _pendingBundle = bundle;
            return;
        }

        TransferAbility(bundle.Communicator);
        bundle.Communicator.RegisterAbility<ILifecycleAwareAbility>(_lifecycleAbility);
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }

    //
    // Navigation
    //

    private bool GoBack()
    {
        if (ContentFrame == null)
        {
            return false;
        }

        if (!ContentFrame.CanGoBack)
        {
            return false;
        }

        ContentFrame.GoBack();
        return true;
    }

    private bool GoForward()
    {
        if (ContentFrame == null)
        {
            return false;
        }

        if (!ContentFrame.CanGoForward)
        {
            return false;
        }

        ContentFrame.GoForward();
        return true;
    }

    private void OnPageChanged(object sender, NavigationEventArgs e)
    {
        _ability.SendLeavingEvent();
        _ability.ClearSubscriptions();

        _currentBundle = (NavigationBundle)e.Parameter;
        GetMainPageAbility().SetCurrentPageInfo(_currentBundle.Url, _currentBundle.PageTrait);

        bool isHomePage = _currentBundle.PageTrait is HomePageTrait;
        bool isReaderPage = _currentBundle.PageTrait is ReaderPageTrait;
        ViewModel.IsHomePage = isHomePage;
        SearchBox.Visibility = isReaderPage ? Visibility.Collapsed : Visibility.Visible;
        SpCenterButtons.Visibility = isReaderPage ? Visibility.Visible : Visibility.Collapsed;
        SetSearchBox("");

        bool immersiveMode = _currentBundle.PageTrait.ImmersiveMode();
        if (immersiveMode != _immersiveMode)
        {
            _immersiveMode = immersiveMode;
            UpdateContentFramePlacement();
            UpdateTopPadding();
        }
    }

    private void UpdateContentFramePlacement()
    {
        bool success = false;
        success = success || ContentGridNormal.Children.Remove(ContentFrame);
        success = success || ContentGridImmersive.Children.Remove(ContentFrame);
        if (!success)
        {
            Logger.F(TAG, "Failed to remove ContentFrame from parent panel.");
            return;
        }

        if (_immersiveMode)
        {
            ContentGridImmersive.Children.Add(ContentFrame);
        }
        else
        {
            ContentGridNormal.Children.Add(ContentFrame);
        }
    }

    //
    // Top tile
    //

    private void OnTopTileSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _navigationBarHeight = e.NewSize.Height;
        GetEventBus().With<double>(EventId.NavigationBarHeightChange).Emit(_navigationBarHeight);
    }

    private void UpdateTopPadding()
    {
        if (_immersiveMode)
        {
            TopTile.Margin = new Thickness(0, _rootTabHeight, 0, 0);
        }
        else
        {
            TopTile.Margin = new Thickness(0, 0, 0, 0);
        }
    }

    //
    // Search box
    //

    public void SetSearchBox(string keywords)
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.Text = keywords;
    }

    private void OnSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _ability.SendSearchTextChangeEvent(sender.Text);
    }

    private void OnSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string queryText = args.QueryText;
        if (queryText.Trim().Length == 0)
        {
            return;
        }

        if (DebugCommand.TryExecute(queryText))
        {
            return;
        }

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, queryText);
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    //
    // Sidebar
    //

    private void OnOpenSidebarClick(object sender, RoutedEventArgs e)
    {
        NavigationPageSidePane.IsPaneOpen = !NavigationPageSidePane.IsPaneOpen;
    }

    private void NavigationPageSidePane_PaneOpenedOrClosed(SplitView sender, object args)
    {
        bool opened = NavigationPageSidePane.IsPaneOpen;
        SyncSidebarOpenState(opened);
        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SIDE_PANE_OPENED, opened);
    }

    private void NavigationPageSidePane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double newWidth = NavigationPageSidePane.OpenPaneLength;
        KVDatabase.Default.SetDouble(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SIDE_PANE_WIDTH, newWidth);
    }

    private void RightSidePane_PinStateChanged(SidePane sender, bool pinned)
    {
        NavigationPageSidePane.DisplayMode = pinned ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
    }

    private void SetSidePaneOpen(bool open, bool force)
    {
        if (open)
        {
            NavigationPageSidePane.IsPaneOpen = true;
        }
        else if (force || !RightSidePane.Pinned)
        {
            NavigationPageSidePane.IsPaneOpen = false;
        }
    }

    private void SyncSidebarOpenState(bool opened)
    {
        ViewModel.UpdateSidebarButton(opened);
        _sidePaneLifecycleAbility.SetCustomState("Pane", opened ? ILifecycle.State.Resumed : ILifecycle.State.Started);
    }

    //
    // Buttons
    //

    private void OnGoBackClick(object sender, RoutedEventArgs e)
    {
        _ = GoBack();
    }

    private void OnGoForwardClick(object sender, RoutedEventArgs e)
    {
        _ = GoForward();
    }

    private void OnHomeClick(object sender, RoutedEventArgs e)
    {
        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
        GetMainPageAbility().OpenInCurrentTab(route);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _ability.SendRefreshEvent();
    }

    private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
    {
        SetIsFavorite(!_isFavorite);
    }

    private void OnComicInfoClick(object sender, RoutedEventArgs e)
    {
        _ability.SendExpandInfoPaneEvent();
    }

    private void SetIsFavorite(bool isFavorite)
    {
        _isFavorite = isFavorite;
        FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
        string toolTip = isFavorite ? StringResourceProvider.Instance.RemoveFromFavorites :
            StringResourceProvider.Instance.AddToFavorites;
        ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
        _ability.SendFavoriteChangedEvent(isFavorite);
    }

    private void AbtbPreviewButton_Checked(object sender, RoutedEventArgs e)
    {
        _ability.SendGridViewModeChangedEvent(true);
    }

    private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _ability.SendGridViewModeChangedEvent(false);
    }

    private void MainReaderSettingPanel_DataChanged(ReaderSettingDataModel model)
    {
        _ability.SendReaderSettingsChangedEvent(model);
    }

    private void SetGridViewModeEnabled(bool enabled)
    {
        AbtbPreviewButton.IsChecked = enabled;
    }

    private void ReaderSettingFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
    {
        args.Cancel = MainReaderSettingPanel.ActionInProgress;
    }

    //
    // Pointer events
    //

    private PointerPoint? _lastPointerPoint;

    private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _lastPointerPoint = e.GetCurrentPoint(sender as UIElement);
    }

    private void OnPagePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_lastPointerPoint == null)
        {
            return;
        }

        if (_lastPointerPoint.Properties.IsXButton1Pressed)
        {
            _ = GoBack();
        }
        else if (_lastPointerPoint.Properties.IsXButton2Pressed)
        {
            _ = GoForward();
        }

        _lastPointerPoint = null;
    }

    //
    // Utilities
    //

    private IMainWindowAbility GetMainWindowAbility()
    {
        return GetAbility<IMainWindowAbility>()!;
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private void TransferAbility(PageCommunicator communicator)
    {
        communicator.RegisterAbility(GetAbility<IMainWindowAbility>()!);
        communicator.RegisterAbility(GetMainPageAbility());
        communicator.RegisterAbility<INavigationPageAbility>(_ability);
    }

    //
    // Types
    //

    private class SidePaneHandler(NavigationPage page) : SidePane.ISidePaneHandler
    {
        public int GetWindowId()
        {
            return page.WindowId;
        }

        public void TransferAbility(NavigationBundle bundle)
        {
            page.TransferAbility(bundle.Communicator);
            bundle.Communicator.RegisterAbility<ILifecycleAwareAbility>(page._sidePaneLifecycleAbility);
        }
    }

    private class NavigationPageAbility(NavigationPage parent) : INavigationPageAbility
    {
        private const string EVENT_LEAVING = "Leaving";
        private const string EVENT_REFRESH = "Refresh";
        private const string EVENT_EXPAND_INFO_PANE = "ExpandInfoPane";
        private const string EVENT_FAVORITE_CHANGED = "FavoriteChanged";
        private const string EVENT_GRID_VIEW_MODE_CHANGED = "GridViewModeChanged";
        private const string EVENT_READER_SETTINGS_CHANGED = "ReaderSettingsChanged";
        private const string EVENT_SEARCH_TEXT_CHANGED = "SearchTextChanged";

        private readonly WeakReference<NavigationPage> _parent = new(parent);
        private readonly EventBus _eventBus = new();

        public void ClearSubscriptions()
        {
            _eventBus.Clear();
        }

        public void SetExternalComic(bool isExternal)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.AbbAddToFavorite.IsEnabled = !isExternal;
        }

        public void SetFavorite(bool isFavorite)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.SetIsFavorite(isFavorite);
        }

        public void SetGridViewMode(bool enabled)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.SetGridViewModeEnabled(enabled);
        }

        public void SetSidePaneOpen(bool open, bool force)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.SetSidePaneOpen(open, force: force);
        }

        public void SetReaderSettings(ComicModel comic)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.MainReaderSettingPanel.SetComic(comic);
        }

        public void SetSearchBox(string text)
        {
            if (!_parent.TryGetTarget(out NavigationPage? parent))
            {
                return;
            }

            parent.SetSearchBox(text);
        }

        public void RegisterLeavingHandler(ILifecycleOwner owner, INavigationPageAbility.CommonEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_LEAVING).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendLeavingEvent()
        {
            _eventBus.With<bool>(EVENT_LEAVING).Emit(true);
        }

        public void RegisterRefreshHandler(ILifecycleOwner owner, INavigationPageAbility.CommonEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_REFRESH).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendRefreshEvent()
        {
            _eventBus.With<bool>(EVENT_REFRESH).Emit(true);
        }

        public void RegisterExpandInfoPaneHandler(ILifecycleOwner owner, INavigationPageAbility.CommonEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_EXPAND_INFO_PANE).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendExpandInfoPaneEvent()
        {
            _eventBus.With<bool>(EVENT_EXPAND_INFO_PANE).Emit(true);
        }

        public void RegisterFavoriteChangedEventHandler(ILifecycleOwner owner, INavigationPageAbility.FavoriteChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).Observe(owner, delegate (bool isFavorite)
            {
                handler(isFavorite);
            });
        }

        public void SendFavoriteChangedEvent(bool isFavorite)
        {
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).Emit(isFavorite);
        }

        public void RegisterGridViewModeChangedHandler(ILifecycleOwner owner, INavigationPageAbility.GridViewModeChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).Observe(owner, delegate (bool isGridViewMode)
            {
                handler(isGridViewMode);
            });
        }

        public void SendGridViewModeChangedEvent(bool isGridViewMode)
        {
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).Emit(isGridViewMode);
        }

        public void RegisterReaderSettingsChangedEventHandler(ILifecycleOwner owner, INavigationPageAbility.ReaderSettingsChangedEventHandler handler)
        {
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).Observe(owner, delegate (ReaderSettingDataModel settings)
            {
                handler(settings);
            });
        }

        public void SendReaderSettingsChangedEvent(ReaderSettingDataModel settings)
        {
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).Emit(settings.Clone());
        }

        public void RegisterSearchTextChangeHandler(ILifecycleOwner owner, INavigationPageAbility.SearchTextChangeEventHandler handler)
        {
            _eventBus.With<string>(EVENT_SEARCH_TEXT_CHANGED).Observe(owner, delegate (string text)
            {
                handler(text);
            });
        }

        public void SendSearchTextChangeEvent(string text)
        {
            _eventBus.With<string>(EVENT_SEARCH_TEXT_CHANGED).Emit(text);
        }
    }
}
