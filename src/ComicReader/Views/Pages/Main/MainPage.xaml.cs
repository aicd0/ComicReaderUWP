// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Localization;
using ComicReader.Common.Misc;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;
using ComicReader.Views.AppWindows.Main;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Views.Pages.Main;

internal sealed partial class MainPage : BasePage
{
    private const string TAG = nameof(MainPage);

    private static int _highestTabId = 0;

    public MainPageViewModel ViewModel { get; } = new();

    //
    // Member variables
    //

    private Grid? _tabContainerGrid;
    private Storyboard? _titleBarAnimation;
    private long _tabContainerGridOpacityListenerToken = 0;
    private bool _contentPresenterLoaded = false;
    private bool _immersiveMode = false;
    private bool _titleBarVisible = true;
    private bool _isFavorite = false;
    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;
    private double _sidePaneWidth = 0;
    private bool _sidePaneOpened = false;
    private bool _sidePanePinned = false;

    private readonly List<TabInfo> _tabs = [];
    private TabInfo? _currentTab;

    private readonly MainPageAbilityForSidebar _abilityForSidebar;

    //
    // Properties
    //

    public ITabInfo? CurrentTab => _currentTab;

    private MainWindow? _currentWindow;
    private MainWindow CurrentWindow => _currentWindow!;

    //
    // Constructors
    //

    public MainPage()
    {
        InitializeComponent();

        _abilityForSidebar = new(this);
        RightSidePane.Initialize(new SidePaneHandler(this));
        ContentGrid.Background = AppearanceManager.Instance.GetThemeBackground();
    }

    //
    // Public Methods
    //

    /// <summary>
    /// Must be called from the UI thread.
    /// </summary>
    public void Open(Route route, int tabId)
    {
        LoadTabNoLock(tabId, route, true);
    }

    /// <summary>
    /// Closes all currently open tabs and performs any necessary cleanup. Must be called from the UI thread.
    /// </summary>
    public void CloseAllTabs()
    {
        while (_tabs.Count > 0)
        {
            CloseTabInternalNoLock(_tabs[0]);
        }
    }

    /// <summary>
    /// Must be called from the UI thread.
    /// </summary>
    public LastTabStatusJsonModel GetTabStatus()
    {
        TabStatusModel model = new()
        {
            SelectedIndex = RootTabView.SelectedIndex
        };

        foreach (TabInfo item in _tabs)
        {
            model.Tabs.Add(new TabModel { Url = item.CurrentUrl });
        }

        LastTabStatusJsonModel jsonModel = new()
        {
            SelectedIndex = model.SelectedIndex,
            Tabs = []
        };

        foreach (TabModel tab in model.Tabs)
        {
            jsonModel.Tabs.Add(new TabJsonModel { Url = tab.Url });
        }

        return jsonModel;
    }

    public void RestoreTabStatus(LastTabStatusJsonModel? jsonModel)
    {
        TabStatusModel? model = null;
        if (jsonModel is not null)
        {
            model = new();
            if (jsonModel.Tabs is not null)
            {
                foreach (TabJsonModel? tab in jsonModel.Tabs)
                {
                    if (tab is null || string.IsNullOrEmpty(tab.Url))
                    {
                        continue;
                    }

                    model.Tabs.Add(new TabModel { Url = tab.Url });
                }
            }

            if (model.Tabs.Count == 0)
            {
                model = null;
            }
            else
            {
                model.SelectedIndex = Math.Clamp(jsonModel.SelectedIndex ?? -1, 0, model.Tabs.Count - 1);
            }
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            if (model is not null)
            {
                for (int i = 0; i < model.Tabs.Count; ++i)
                {
                    TabModel tab = model.Tabs[i];
                    if (string.IsNullOrEmpty(tab.Url))
                    {
                        continue;
                    }

                    var route = Route.Create(tab.Url);
                    LoadTabNoLock(-1, route, i == model.SelectedIndex);
                }
            }

            EnsureInitialTabNoLock();
        });
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        _currentWindow = App.Instance.WindowManager.GetWindow(WindowId);

        CurrentWindow.SetTitleBar(MainTitleBar);
        AppWindowTitleBar titleBar = CurrentWindow.AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = MainTitleBar.ButtonBackground?.Color;
        titleBar.ButtonForegroundColor = MainTitleBar.ButtonForeground?.Color;
        titleBar.ButtonInactiveBackgroundColor = MainTitleBar.ButtonInactiveBackground?.Color;
        titleBar.ButtonInactiveForegroundColor = MainTitleBar.ButtonInactiveForeground?.Color;
        titleBar.ButtonHoverBackgroundColor = MainTitleBar.ButtonHoverBackground?.Color;
        titleBar.ButtonHoverForegroundColor = MainTitleBar.ButtonHoverForeground?.Color;
        titleBar.ButtonPressedBackgroundColor = MainTitleBar.ButtonPressedBackground?.Color;
        titleBar.ButtonPressedForegroundColor = MainTitleBar.ButtonPressedForeground?.Color;

        OnscreenLogger.Initialize();
        ViewModel.Initialize(PageActionHandler);
        ObserveData();
        SyncSidebarOpenState(NavigationPageSidePane.IsPaneOpen, initialSync: true);
    }

    protected override void OnResume()
    {
        base.OnResume();

        MainReaderSettingPanel.SetWindowId(WindowId);
        ViewModel.UpdateMoreMenuItems();
        NavigationPageSidePane.OpenPaneLength = KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault<double>(DatabaseEntry.KV_KEY_APP_SIDE_PANE_WIDTH, 380);
        RightSidePane.RestoreLastStatus();

        if (_sidePanePinned && KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_SIDE_PANE_OPENED, false))
        {
            SetSidePaneOpenState(true, force: true);
        }
    }

    protected override void OnStop()
    {
        base.OnStop();

        ViewModel.OnStop();
    }

    private void ObserveData()
    {
        BusyStateManager.Busy.ObserveSticky(this, busy =>
        {
            ViewModel.IsBusy = busy;
        });

        OnscreenLogger.Started.ObserveSticky(this, ViewModel.SetLogStarted);
        OnscreenLogger.Visible.ObserveSticky(this, ViewModel.SetLogVisibility);

        ComicHandle.IsScanningLibrary.ObserveSticky(this, scanning =>
        {
            ViewModel.Refreshing = scanning;
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            TopTile.Opacity = opacity;
            NavigationPageSidePane.Opacity = opacity;
            if (_tabContainerGrid != null)
            {
                _tabContainerGrid.Opacity = opacity;
                FullscreenButtonGrid.Opacity = opacity;
                _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
            }
        });

        GetEventBus().With<int>(EventId.CloseTab).Observe(this, CloseTabNoLock);

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, isFullscreen =>
        {
            ViewModel.IsFullscreen = isFullscreen;
        });

        _abilityForSidebar.GetLifecycleAbility().Observe(this);
    }

    //
    // Tab Management
    //

    private void EnsureInitialTabNoLock()
    {
        if (_tabs.Count == 0)
        {
            var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
            LoadTabNoLock(-1, route, true);
        }
    }

    private bool LoadTabNoLock(int tabId, Route route, bool select)
    {
        if (tabId < -1)
        {
            Logger.F(TAG, $"Invalid tab ID {tabId}.");
            return false;
        }

        NavigationBundle? bundle = AppRouter.Process(route);
        if (bundle is null)
        {
            Logger.F(TAG, $"Failed to process route: {route.Url}");
            return false;
        }

        if (!bundle.PageTrait.SupportMultiInstance())
        {
            foreach (TabInfo tab in _tabs)
            {
                if (tab.CurrentUrl == bundle.Url)
                {
                    if (select)
                    {
                        RootTabView.SelectedItem = tab.Item;
                    }

                    return true;
                }
            }
        }

        bool newTab = tabId == -1;
        if (newTab)
        {
            tabId = AddTabNoLock(bundle);
        }

        TabInfo? tabInfo = GetTabInfoNoLock(tabId);
        if (tabInfo == null)
        {
            Logger.F(TAG, $"Failed to find tab info for ID {tabId}.");
            return false;
        }

        if (select)
        {
            RootTabView.SelectedItem = tabInfo.Item;
        }

        if (!newTab && tabInfo.CurrentUrl == bundle.Url)
        {
            return true;
        }

        TransferAbility(bundle.Communicator, tabInfo);
        var frame = (Frame)tabInfo.Item.Content;
        frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        return true;
    }

    private int AddTabNoLock(NavigationBundle bundle)
    {
        int tabId = Interlocked.Increment(ref _highestTabId);
        var frame = new Frame();
        var item = new TabViewItem
        {
            Header = StringResource.Untitled,
            Content = frame,
        };
        MainPageAbilityForTab ability = new(this, tabId);
        NavigationPageAbility navigationBarAbility = new(this);
        TabInfo tabInfo = new()
        {
            Id = tabId,
            Item = item,
            Ability = ability,
            NavigationBarAbility = navigationBarAbility,
            CurrentPageTrait = bundle.PageTrait,
            CurrentUrl = bundle.Url,
        };
        tabInfo.NavigatedHandler = (sender, e) =>
        {
            var newBundle = (NavigationBundle)e.Parameter;
            tabInfo.NavigationBarAbility.ClearStates();
            tabInfo.CurrentPageTrait = newBundle.PageTrait;
            tabInfo.CurrentUrl = newBundle.Url;
            if (_currentTab is not null && tabInfo.Id == _currentTab.Id)
            {
                OnPageChanged();
            }
        };
        frame.Navigated += tabInfo.NavigatedHandler;
        _tabs.Add(tabInfo);
        RootTabView.TabItems.Add(item);
        return tabId;
    }

    private void CloseTabNoLock(int tabId)
    {
        if (tabId < 0)
        {
            return;
        }

        TabInfo? closingTab = null;
        for (int i = 0; i < _tabs.Count; ++i)
        {
            TabInfo tabInfo = _tabs[i];
            if (tabInfo.Id == tabId)
            {
                closingTab = tabInfo;
                break;
            }
        }

        if (closingTab == null)
        {
            return;
        }

        CloseTabInternalNoLock(closingTab);

        if (_tabs.Count <= 0)
        {
            CurrentWindow?.Close();
        }

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
    }

    private void CloseTabInternalNoLock(TabInfo tabInfo)
    {
        ((Frame)tabInfo.Item.Content).Navigated -= tabInfo.NavigatedHandler;
        tabInfo.NavigatedHandler = null;
        tabInfo.Ability.GetLifecycleAbility().SetCustomState("Tab", ILifecycle.State.Stopped);
        _tabs.Remove(tabInfo);
        RootTabView.TabItems.Remove(tabInfo.Item);
    }

    private TabInfo? GetTabInfoNoLock(int tabId)
    {
        foreach (TabInfo tab in _tabs)
        {
            if (tab.Id == tabId)
            {
                return tab;
            }
        }

        return null;
    }

    //
    // Tab View
    //

    private void OnAddTabButtonClicked(TabView sender, object args)
    {
        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
        Open(route, -1);
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        int closingTabId = -1;
        for (int i = 0; i < _tabs.Count; ++i)
        {
            TabInfo tabInfo = _tabs[i];
            if (tabInfo.Item == args.Tab)
            {
                closingTabId = tabInfo.Id;
                break;
            }
        }

        CloseTabNoLock(closingTabId);
    }

    private void OnTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }

        TabInfo? lastSelectedTab = _currentTab;
        var newSelectedTabItem = (TabViewItem)e.AddedItems[0];
        TabInfo? newSelectedTab = null;
        foreach (TabInfo tabInfo in _tabs)
        {
            if (tabInfo.Item == newSelectedTabItem)
            {
                newSelectedTab = tabInfo;
            }
        }

        Logger.Assert(newSelectedTab != null, "59496F61DEF5BD3C");
        _currentTab = newSelectedTab;

        lastSelectedTab?.Ability.SendTabUnselectedEvent();
        OnPageChanged();
    }

    private void OnRootTabViewTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        TabInfo? draggingTab = null;
        foreach (TabInfo tabInfo in _tabs)
        {
            if (tabInfo.Item == args.Tab)
            {
                draggingTab = tabInfo;
            }
        }
        if (draggingTab == null)
        {
            Logger.AssertNotReachHere("96A351AFF8B07EB6");
            return;
        }

        args.Data.Properties.Add("windowId", WindowId);
        args.Data.Properties.Add("tabId", draggingTab.Id);
        args.Data.Properties.Add("url", draggingTab.CurrentUrl);
    }

    private void OnRootTabViewDrop(object sender, DragEventArgs e)
    {
        // Handle tab drag-drop
        if (e.DataView.Properties.TryGetValue("windowId", out object windowIdObj) && windowIdObj is int sourceWindowId &&
            e.DataView.Properties.TryGetValue("tabId", out object tabIdObj) && tabIdObj is int sourceTabId &&
            e.DataView.Properties.TryGetValue("url", out object urlObj) && urlObj is string url)
        {
            if (sourceWindowId != WindowId)
            {
                App.Instance.WindowManager.GetEventBus(sourceWindowId).With<int>(EventId.CloseTab).Emit(sourceTabId);
                LoadTabNoLock(-1, Route.Create(url), true);
                EnsureInitialTabNoLock();
            }

            return;
        }

        // Handle file drop
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            CoroutineUtils.Start(async () =>
            {
                IReadOnlyList<Windows.Storage.IStorageItem> items = await e.DataView.GetStorageItemsAsync();
                foreach (Windows.Storage.IStorageItem? item in items)
                {
                    if (item is Windows.Storage.StorageFile file)
                    {
                        App.Instance.OnCommandLine(CurrentWindow, [item.Path]);
                    }
                }
            });

            return;
        }
    }

    private void OnRootTabViewDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void OnRootTabViewTabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
    {
        TabViewItem? tab = args.Tab;
        if (tab is null)
        {
            return;
        }

        TabInfo? removingTab = null;
        foreach (TabInfo tabInfo in _tabs)
        {
            if (tabInfo.Item == tab)
            {
                removingTab = tabInfo;
            }
        }

        if (removingTab == null)
        {
            Logger.AssertNotReachHere("F40D97E40039ADF7");
            return;
        }

        if (_tabs.Count <= 1)
        {
            return;
        }

        _tabs.Remove(removingTab);
        RootTabView.TabItems.Remove(tab);
        MainWindow.Open(url: removingTab.CurrentUrl);
    }

    private void OnPageChanged()
    {
        TabInfo? tabInfo = _currentTab;
        if (tabInfo is not null)
        {
            OnPageChangedInternal(tabInfo);
        }

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
    }

    private void OnPageChangedInternal(TabInfo tabInfo)
    {
        IPageTrait pageTrait = tabInfo.CurrentPageTrait;
        bool immersiveMode = pageTrait.ImmersiveMode();
        bool isHomePage = pageTrait is HomePageTrait;
        bool isReaderPage = pageTrait is ReaderPageTrait;

        ViewModel.IsHomePage = isHomePage;
        ViewModel.CanGoBack = ((Frame)tabInfo.Item.Content).CanGoBack;
        ViewModel.CanGoForward = ((Frame)tabInfo.Item.Content).CanGoForward;
        SearchBox.Visibility = isReaderPage ? Visibility.Collapsed : Visibility.Visible;
        SpCenterButtons.Visibility = isReaderPage ? Visibility.Visible : Visibility.Collapsed;

        if (immersiveMode != _immersiveMode)
        {
            _immersiveMode = immersiveMode;
            UpdateContentFramePlacement();
            RootTabView.Background = _immersiveMode ?
                (Brush)Application.Current.Resources["TitleBarBackground"] :
                new SolidColorBrush(Colors.Transparent);
        }

        if (!immersiveMode)
        {
            ShowOrHideTitleBar(true, transitionAnimation: false);
        }

        tabInfo.NavigationBarAbility.RestoreStates();
    }

    private void OnTabContainerGridLoaded(object sender, RoutedEventArgs e)
    {
        _tabContainerGrid = (Grid)sender;

        _tabContainerGridOpacityListenerToken = _tabContainerGrid.RegisterPropertyChangedCallback(OpacityProperty, (sender, dp) =>
        {
            if (!IsStarted)
            {
                return;
            }

            GetEventBus().With<double>(EventId.TitleBarOpacity).Emit(_tabContainerGrid.Opacity);
        });
    }

    private void OnTabContainerGridUnloaded(object sender, RoutedEventArgs e)
    {
        _tabContainerGrid?.UnregisterPropertyChangedCallback(OpacityProperty, _tabContainerGridOpacityListenerToken);
        _tabContainerGrid = null;
    }

    private void OnTabContentPresenterLoaded(object sender, RoutedEventArgs e)
    {
        if (_contentPresenterLoaded)
        {
            return;
        }

        _contentPresenterLoaded = true;
        var tabContentPresenter = (ContentPresenter)sender;
        var parent = (Grid)tabContentPresenter.Parent;
        parent.Children.Remove(tabContentPresenter);
        ContentGrid.Children.Add(tabContentPresenter);
    }

    //
    // Title Bar Animation
    //

    private void ShowOrHideTitleBar(bool show, bool transitionAnimation)
    {
        UIElement? targetElement = _tabContainerGrid;
        if (_currentTab == null || show == _titleBarVisible || targetElement == null)
        {
            return;
        }

        if (!show && !_currentTab.CurrentPageTrait.ImmersiveMode())
        {
            // Only hide the title bar when the current page supports immersive mode.
            return;
        }

        _titleBarVisible = show;
        double targetOpacity = show ? 1.0 : 0.0;

        if (_titleBarAnimation != null)
        {
            _titleBarAnimation.Stop();
            _titleBarAnimation = null;
        }

        if (transitionAnimation)
        {
            DoubleAnimation animation = new()
            {
                From = targetElement.Opacity,
                To = targetOpacity,
                Duration = TimeSpan.FromSeconds(0.2),
            };

            Storyboard.SetTarget(animation, targetElement);
            Storyboard.SetTargetProperty(animation, "Opacity");
            Storyboard storyboard = new();
            storyboard.Children.Add(animation);
            storyboard.Begin();
            _titleBarAnimation = storyboard;
        }
        else
        {
            targetElement.Opacity = targetOpacity;
        }

        DispatchToAllTabs(delegate (MainPageAbility ability)
        {
            ability.SendTitleBarVisibilityChangedEvent(show);
        });
    }

    //
    // Navigation
    //

    private bool GoBack()
    {
        Frame? contentFrame = GetCurrentContentFrame();
        if (contentFrame is null || !contentFrame.CanGoBack)
        {
            return false;
        }

        contentFrame.GoBack();
        return true;
    }

    private bool GoForward()
    {
        Frame? contentFrame = GetCurrentContentFrame();
        if (contentFrame is null || !contentFrame.CanGoForward)
        {
            return false;
        }

        contentFrame.GoForward();
        return true;
    }

    private void UpdateContentFramePlacement()
    {
        bool success = false;
        success = success || ContentGridNormal.Children.Remove(ContentGrid);
        success = success || ContentGridImmersive.Children.Remove(ContentGrid);
        if (!success)
        {
            Logger.F(TAG, "Failed to remove ContentGrid from parent panel.");
            return;
        }

        if (_immersiveMode)
        {
            ContentGridImmersive.Children.Add(ContentGrid);
        }
        else
        {
            ContentGridNormal.Children.Add(ContentGrid);
        }
    }

    private Frame? GetCurrentContentFrame()
    {
        TabInfo? tabInfo = _currentTab;
        if (tabInfo is null)
        {
            Logger.F(TAG, "GetCurrentContentFrame: Current tab not set.");
            return null;
        }

        return (Frame)tabInfo.Item.Content;
    }

    private NavigationPageAbility? GetCurrentNavigationBarAbility()
    {
        TabInfo? tabInfo = _currentTab;
        if (tabInfo is null)
        {
            Logger.F(TAG, "GetCurrentNavigationBarAbility: Current tab not set.");
            return null;
        }

        return tabInfo.NavigationBarAbility;
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
        GetCurrentNavigationBarAbility()?.SendSearchTextChangeEvent(sender.Text);
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
        _abilityForSidebar.OpenInCurrentTab(route);
    }

    //
    // Sidebar
    //

    private void OnOpenSidebarClick(object sender, RoutedEventArgs e)
    {
        SetSidePaneOpenState(!_sidePaneOpened, force: true);
    }

    private void NavigationPageSidePane_PaneOpenedOrClosed(SplitView sender, object args)
    {
        SyncSidebarOpenState(NavigationPageSidePane.IsPaneOpen);
    }

    private void RightSidePane_PinStateChanged(SidePaneView sender, bool pinned)
    {
        _sidePanePinned = pinned;
        NavigationPageSidePane.DisplayMode = pinned ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
        DispatchRightOverlayWidthChangeEvent();
    }

    private void NavigationPageSidePane_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double newWidth = NavigationPageSidePane.OpenPaneLength;
        if (_sidePaneWidth == newWidth)
        {
            return;
        }

        _sidePaneWidth = newWidth;
        DispatchRightOverlayWidthChangeEvent();
        KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SIDE_PANE_WIDTH, newWidth);
    }

    private void SetSidePaneOpenState(bool open, bool force)
    {
        if (open == _sidePaneOpened)
        {
            return;
        }

        if (open)
        {
            NavigationPageSidePane.IsPaneOpen = true;
        }
        else if (force || !RightSidePane.Pinned)
        {
            NavigationPageSidePane.IsPaneOpen = false;
        }
        else
        {
            return;
        }

        SyncSidebarOpenState(open);
    }

    private void SyncSidebarOpenState(bool opened, bool initialSync = false)
    {
        if (!initialSync && opened == _sidePaneOpened)
        {
            return;
        }

        _sidePaneOpened = opened;
        ViewModel.UpdateSidebarButton(opened);
        _abilityForSidebar.GetLifecycleAbility().SetCustomState("Pane", opened ? ILifecycle.State.Resumed : ILifecycle.State.Started);
        DispatchRightOverlayWidthChangeEvent();

        if (!initialSync)
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SIDE_PANE_OPENED, opened);
        }
    }

    //
    // Fullscreen
    //

    private void FullscreenButtonGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ShowOrHideTitleBar(true, transitionAnimation: true);
    }

    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainWindowAbility().EnterFullscreen();
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainWindowAbility().ExitFullscreen();
    }

    //
    // Buttons
    //

    private void OnGoBackClick(object sender, RoutedEventArgs e)
    {
        GoBack();
    }

    private void OnGoForwardClick(object sender, RoutedEventArgs e)
    {
        GoForward();
    }

    private void OnHomeClick(object sender, RoutedEventArgs e)
    {
        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
        _abilityForSidebar.OpenInCurrentTab(route);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        GetCurrentNavigationBarAbility()?.SendRefreshEvent();
    }

    private void OnAddToFavoritesClick(object sender, RoutedEventArgs e)
    {
        SetIsFavorite(!_isFavorite);
    }

    private void OnComicInfoClick(object sender, RoutedEventArgs e)
    {
        GetCurrentNavigationBarAbility()?.SendExpandInfoPaneEvent();
    }

    private void SetIsFavorite(bool isFavorite)
    {
        _isFavorite = isFavorite;
        FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;
        string toolTip = isFavorite ? StringResourceProvider.Instance.RemoveFromFavorites :
            StringResourceProvider.Instance.AddToFavorites;
        ToolTipService.SetToolTip(AbbAddToFavorite, toolTip);
        GetCurrentNavigationBarAbility()?.SendFavoriteChangedEvent(isFavorite);
    }

    private void AbtbPreviewButton_Checked(object sender, RoutedEventArgs e)
    {
        GetCurrentNavigationBarAbility()?.SendGridViewModeChangedEvent(true);
    }

    private void AbtbPreviewButton_Unchecked(object sender, RoutedEventArgs e)
    {
        GetCurrentNavigationBarAbility()?.SendGridViewModeChangedEvent(false);
    }

    private void MainReaderSettingPanel_DataChanged(ReaderSettingDataModel model)
    {
        GetCurrentNavigationBarAbility()?.SendReaderSettingsChangedEvent(model);
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
            GoBack();
        }
        else if (_lastPointerPoint.Properties.IsXButton2Pressed)
        {
            GoForward();
        }

        _lastPointerPoint = null;
    }

    //
    // Size Change Events
    //

    private void OnTabContainerGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_rootTabHeight == e.NewSize.Height)
        {
            return;
        }

        _rootTabHeight = e.NewSize.Height;
        TopTile.Margin = new Thickness(0, _rootTabHeight, 0, 0);
        DispatchTopOverlayHeightChangeEvent();
    }

    private void OnTopTileSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_navigationBarHeight == e.NewSize.Height)
        {
            return;
        }

        _navigationBarHeight = e.NewSize.Height;
        DispatchTopOverlayHeightChangeEvent();
    }

    private void DispatchTopOverlayHeightChangeEvent()
    {
        GetEventBus().With<double>(EventId.TopOverlayHeight).Emit(_rootTabHeight + _navigationBarHeight);
    }

    private void DispatchRightOverlayWidthChangeEvent()
    {
        GetEventBus().With<double>(EventId.RightOverlayWidth).Emit(_sidePaneOpened && _sidePanePinned ? _sidePaneWidth : 0);
    }

    //
    // Key Events
    //

    private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        bool handled = false;
        bool ctrlDown = args.KeyboardAccelerator.Modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
        switch (args.KeyboardAccelerator.Key)
        {
            case Windows.System.VirtualKey.Escape:
                GetMainWindowAbility().ExitFullscreen();
                handled = true;
                break;
            case Windows.System.VirtualKey.F10:
                if (ctrlDown)
                {
                    OnscreenLogger.StartOrPause();
                    handled = true;
                }
                break;
            case Windows.System.VirtualKey.F11:
                if (ctrlDown)
                {
                    OnscreenLogger.ShowOrHide();
                    handled = true;
                }
                break;
        }

        if (handled)
        {
            args.Handled = true;
        }
    }

    //
    // Utilities
    //

    private IMainWindowAbility GetMainWindowAbility()
    {
        return GetAbility<IMainWindowAbility>()!;
    }

    private void DispatchToAllTabs(Action<MainPageAbility> action)
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            foreach (TabInfo tab in _tabs)
            {
                action(tab.Ability);
            }
        });
    }

    //
    // Page Ability
    //

    private void TransferAbility(PageCommunicator communicator)
    {
        communicator.RegisterAbility<ILifecycleAwareAbility>(_abilityForSidebar);
        communicator.RegisterAbility(GetMainWindowAbility());
        communicator.RegisterAbility<IMainPageAbility>(_abilityForSidebar);
    }

    private void TransferAbility(PageCommunicator communicator, TabInfo tabInfo)
    {
        tabInfo.Ability.GetLifecycleAbility().Observe(this);
        communicator.RegisterAbility<ILifecycleAwareAbility>(tabInfo.Ability);
        communicator.RegisterAbility(GetMainWindowAbility());
        communicator.RegisterAbility<IMainPageAbility>(tabInfo.Ability);
        communicator.RegisterAbility<IMainPageAbilityForTab>(tabInfo.Ability);
        communicator.RegisterAbility<INavigationPageAbility>(tabInfo.NavigationBarAbility);
    }

    abstract class MainPageAbility(MainPage parent) : IMainPageAbility, ILifecycleAwareAbility
    {
        private const string EVENT_TAB_UNSELECTED = "TabUnselected";

        protected readonly WeakReference<MainPage> _parent = new(parent);
        private readonly LifecycleAwareAbility _lifecycleAbility = new();
        private readonly EventBus _eventBus = new();
        private readonly MutableLiveData<bool> _titleBarVisibilityChangeLiveData = new(parent._titleBarVisible);

        public abstract void OpenInCurrentTab(Route route);

        public void OpenInNewTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                parent.Open(route, -1);
            });
        }

        public void RegisterTabUnselectedHandler(ILifecycleOwner owner, IMainPageAbility.TabUnselectedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_TAB_UNSELECTED).Observe(owner, delegate
            {
                handler();
            });
        }

        public void SendTabUnselectedEvent()
        {
            _eventBus.With<bool>(EVENT_TAB_UNSELECTED).Emit(true);
        }

        public void RegisterTitleBarVisibilityChangedHandler(ILifecycleOwner owner, IMainPageAbility.TitleBarVisibilityChangedEventHandler handler)
        {
            _titleBarVisibilityChangeLiveData.ObserveSticky(owner, delegate (bool visible)
            {
                handler(visible);
            });
        }

        public void ShowOrHideTitleBar(bool show)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.ShowOrHideTitleBar(show, transitionAnimation: true);
        }

        public void SendTitleBarVisibilityChangedEvent(bool visible)
        {
            _titleBarVisibilityChangeLiveData.Emit(visible);
        }

        public void RegisterPageLifecycleHandler(PageLifecycleEventHandler handler)
        {
            _lifecycleAbility.RegisterPageLifecycleHandler(handler);
        }

        public void UnregisterPageLifecycleHandler(PageLifecycleEventHandler handler)
        {
            _lifecycleAbility.UnregisterPageLifecycleHandler(handler);
        }

        public bool GetSidePaneOpenState()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return false;
            }

            return parent._sidePaneOpened;
        }

        public void SetSidePaneOpenState(bool open, bool force)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.SetSidePaneOpenState(open, force: force);
        }

        public LifecycleAwareAbility GetLifecycleAbility()
        {
            return _lifecycleAbility;
        }
    }

    private class MainPageAbilityForTab(MainPage parent, int tabId) : MainPageAbility(parent), IMainPageAbilityForTab
    {
        private readonly int _tabId = tabId;

        public int TabId => _tabId;

        public override void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.LoadTabNoLock(_tabId, route, true);
        }

        public void SetTitle(string title)
        {
            TabInfo? tab = GetTab();
            if (tab == null)
            {
                return;
            }

            tab.Item.Header = title;
        }

        public void SetIcon(IconSource icon)
        {
            TabInfo? tab = GetTab();
            if (tab == null)
            {
                return;
            }

            tab.Item.IconSource = icon;
        }

        private TabInfo? GetTab()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return null;
            }

            return parent.GetTabInfoNoLock(_tabId);
        }
    }

    private class MainPageAbilityForSidebar(MainPage parent) : MainPageAbility(parent)
    {
        public override void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            TabInfo? tabInfo = parent._currentTab;
            if (tabInfo is null)
            {
                return;
            }

            parent.LoadTabNoLock(tabInfo.Id, route, true);
        }
    }

    private class NavigationPageAbility : INavigationPageAbility
    {
        private const string EVENT_REFRESH = "Refresh";
        private const string EVENT_EXPAND_INFO_PANE = "ExpandInfoPane";
        private const string EVENT_FAVORITE_CHANGED = "FavoriteChanged";
        private const string EVENT_GRID_VIEW_MODE_CHANGED = "GridViewModeChanged";
        private const string EVENT_READER_SETTINGS_CHANGED = "ReaderSettingsChanged";
        private const string EVENT_SEARCH_TEXT_CHANGED = "SearchTextChanged";

        private readonly WeakReference<MainPage> _parent;
        private readonly EventBus _eventBus = new();
        private bool _isExternalComic = false;
        private bool _isFavorite = false;
        private bool _gridViewMode = false;
        private string _searchBoxText = string.Empty;
        private bool _fullscreenButtonVisible = true;
        private ComicModel? _readerSettingComic = null;
        private ReaderSettingDataModel? _readerSettings = null;

        public NavigationPageAbility(MainPage parent)
        {
            _parent = new(parent);
            ClearStates();
        }

        public void ClearStates()
        {
            _eventBus.Clear();
            _isExternalComic = false;
            _isFavorite = false;
            _gridViewMode = false;
            _searchBoxText = string.Empty;
            _fullscreenButtonVisible = true;
            _readerSettingComic = null;
            _readerSettings = null;
        }

        public void RestoreStates()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            SetExternalComicInternal(parent);
            SetFavoriteInternal(parent);
            SetGridViewModeInternal(parent);
            SetSearchBoxInternal(parent);
            SetFullscreenButtonVisibleInternal(parent);
            SetReaderSettingsInternal(parent);
        }

        //
        // External Comic Flag
        //

        public void SetExternalComic(bool isExternal)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _isExternalComic = isExternal;
            SetExternalComicInternal(parent);
        }

        private void SetExternalComicInternal(MainPage page)
        {
            page.AbbAddToFavorite.IsEnabled = !_isExternalComic;
        }

        //
        // Favorite Flag
        //

        public void SetFavorite(bool isFavorite)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _isFavorite = isFavorite;
            SetFavoriteInternal(parent);
        }

        public void RegisterFavoriteChangedEventHandler(ILifecycleOwner owner, INavigationPageAbility.FavoriteChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).ObserveSticky(owner, delegate (bool isFavorite)
            {
                handler(isFavorite);
            });
        }

        public void SendFavoriteChangedEvent(bool isFavorite)
        {
            if (isFavorite == _isFavorite)
            {
                return;
            }

            _isFavorite = isFavorite;
            _eventBus.With<bool>(EVENT_FAVORITE_CHANGED).Emit(isFavorite);
        }

        private void SetFavoriteInternal(MainPage page)
        {
            page.SetIsFavorite(_isFavorite);
        }

        //
        // Grid View Mode
        //

        public void SetGridViewMode(bool enabled)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _gridViewMode = enabled;
            SetGridViewModeInternal(parent);
        }

        public void RegisterGridViewModeChangedHandler(ILifecycleOwner owner, INavigationPageAbility.GridViewModeChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).ObserveSticky(owner, delegate (bool isGridViewMode)
            {
                handler(isGridViewMode);
            });
        }

        public void SendGridViewModeChangedEvent(bool isGridViewMode)
        {
            if (isGridViewMode == _gridViewMode)
            {
                return;
            }

            _gridViewMode = isGridViewMode;
            _eventBus.With<bool>(EVENT_GRID_VIEW_MODE_CHANGED).Emit(isGridViewMode);
        }

        private void SetGridViewModeInternal(MainPage page)
        {
            page.SetGridViewModeEnabled(_gridViewMode);
        }

        //
        // Search Box
        //

        public void SetSearchBox(string text)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _searchBoxText = text;
            SetSearchBoxInternal(parent);
        }

        public void RegisterSearchTextChangeHandler(ILifecycleOwner owner, INavigationPageAbility.SearchTextChangeEventHandler handler)
        {
            _eventBus.With<string>(EVENT_SEARCH_TEXT_CHANGED).ObserveSticky(owner, delegate (string text)
            {
                handler(text);
            });
        }

        public void SendSearchTextChangeEvent(string text)
        {
            if (text == _searchBoxText)
            {
                return;
            }

            _searchBoxText = text;
            _eventBus.With<string>(EVENT_SEARCH_TEXT_CHANGED).Emit(text);
        }

        private void SetSearchBoxInternal(MainPage page)
        {
            page.SetSearchBox(_searchBoxText);
        }

        //
        // Fullscreen Button
        //

        public void SetFullscreenButtonVisible(bool visible)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _fullscreenButtonVisible = visible;
            SetFullscreenButtonVisibleInternal(parent);
        }

        private void SetFullscreenButtonVisibleInternal(MainPage page)
        {
            page.FullscreenButtonGrid.Visibility = _fullscreenButtonVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        //
        // Reader Settings
        //

        public void SetReaderSettings(ComicModel comic)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _readerSettingComic = comic;
            SetReaderSettingsInternal(parent);
        }

        public void RegisterReaderSettingsChangedEventHandler(ILifecycleOwner owner, INavigationPageAbility.ReaderSettingsChangedEventHandler handler)
        {
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).ObserveSticky(owner, delegate (ReaderSettingDataModel settings)
            {
                handler(settings);
            });
        }

        public void SendReaderSettingsChangedEvent(ReaderSettingDataModel settings)
        {
            if (_readerSettings is not null && settings == _readerSettings)
            {
                return;
            }

            _readerSettings = settings.Clone();
            _eventBus.With<ReaderSettingDataModel>(EVENT_READER_SETTINGS_CHANGED).Emit(_readerSettings);
        }

        private void SetReaderSettingsInternal(MainPage page)
        {
            if (_readerSettingComic is null)
            {
                return;
            }

            page.MainReaderSettingPanel.SetComic(_readerSettingComic);
        }

        //
        // Refresh
        //

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

        //
        // Comic Info Pane
        //

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
    }

    //
    // Types
    //

    private class SidePaneHandler(MainPage page) : SidePaneView.ISidePaneHandler
    {
        public int GetWindowId()
        {
            return page.WindowId;
        }

        public void TransferAbility(NavigationBundle bundle)
        {
            page.TransferAbility(bundle.Communicator);
        }
    }

    private class TabInfo : ITabInfo
    {
        public required int Id { init; get; }
        public required TabViewItem Item { init; get; }
        public required MainPageAbilityForTab Ability { init; get; }
        public required NavigationPageAbility NavigationBarAbility { init; get; }
        public required string CurrentUrl { get; set; }
        public required IPageTrait CurrentPageTrait { get; set; }
        public NavigatedEventHandler? NavigatedHandler { get; set; }

        //
        // ITabInfo Implmentation
        //

        int ITabInfo.Id => Id;

        string ITabInfo.Title => Item.Header as string ?? string.Empty;
    }

    public class LastTabStatusJsonModel
    {
        [JsonPropertyName("SelectedIndex")]
        public int? SelectedIndex { get; set; }

        [JsonPropertyName("Tabs")]
        public List<TabJsonModel?>? Tabs { get; set; }

        public static LastTabStatusJsonModel FromUrl(string url)
        {
            return new LastTabStatusJsonModel
            {
                SelectedIndex = 0,
                Tabs =
                [
                    new() { Url = url }
                ]
            };
        }
    }

    public class TabJsonModel
    {
        [JsonPropertyName("Url")]
        public string? Url { get; set; }
    }

    private class TabStatusModel
    {
        public int SelectedIndex { get; set; } = -1;
        public List<TabModel> Tabs { get; set; } = [];
    }

    private class TabModel
    {
        public string Url { get; set; } = string.Empty;
    }

    public interface ITabInfo
    {
        int Id { get; }

        string Title { get; }
    }
}
