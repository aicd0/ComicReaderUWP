// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.Views.AppWindows.Main;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReaderUWP.Views.Pages.Main;

internal sealed partial class MainPage : BasePage
{
    private const string TAG = nameof(MainPage);

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

    public void Open(Route route, string targetTabId, string initiateTabId)
    {
        MainThreadUtils.AssertOnMainThread();
        LoadTabNoLock(route, targetTabId, initiateTabId: initiateTabId);
    }

    public void CloseAllTabs()
    {
        MainThreadUtils.AssertOnMainThread();

        while (_tabs.Count > 0)
        {
            CloseTabInternalNoLock(_tabs[0]);
        }
    }

    public LastTabStatusJsonModel? GetTabStatus()
    {
        MainThreadUtils.AssertOnMainThread();

        LastTabStatusJsonModel jsonModel = new()
        {
            SelectedIndex = RootTabView.SelectedIndex,
            Tabs = []
        };

        foreach (TabInfo item in _tabs)
        {
            jsonModel.Tabs.Add(new TabJsonModel
            {
                Id = item.Id,
                Url = item.CurrentBundle.Url,
            });
        }

        if (jsonModel.Tabs.Count == 0)
        {
            return null;
        }

        return jsonModel;
    }

    public void RestoreTabStatus(LastTabStatusJsonModel? jsonModel)
    {
        MainThreadUtils.AssertOnMainThread();

        TabStatusModel? model = null;
        if (jsonModel is not null)
        {
            model = new();
            if (jsonModel.Tabs is not null)
            {
                foreach (TabJsonModel? tab in jsonModel.Tabs)
                {
                    if (tab is null || string.IsNullOrEmpty(tab.Id) || string.IsNullOrEmpty(tab.Url))
                    {
                        continue;
                    }

                    model.Tabs.Add(new TabModel
                    {
                        Id = tab.Id,
                        Url = tab.Url,
                    });
                }
            }

            if (model.Tabs.Count == 0)
            {
                model = null;
            }
            else
            {
                model.SelectedIndex = Math.Clamp(jsonModel.SelectedIndex, 0, model.Tabs.Count - 1);
            }
        }

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
                LoadTabNoLock(route, string.Empty, selectTab: i == model.SelectedIndex, newTabId: tab.Id);
            }
        }

        EnsureInitialTabNoLock();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        _currentWindow = App.Instance.WindowManager.GetWindow(WindowId);
        PageActionHandler.RegisterComponent<IMainPageComponent>(new MainPageComponent(this));

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

        ViewModel.Initialize(PageActionHandler);
        ObserveData();
        SyncSidebarOpenState(NavigationPageSidePane.IsPaneOpen, initialSync: true);
    }

    protected override void OnResume()
    {
        base.OnResume();

        ViewModel.UpdateMoreMenuItems();
        NavigationPageSidePane.OpenPaneLength = AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault<double>(KVNames.KV_KEY_APP_SIDE_PANE_WIDTH, 380);
        RightSidePane.RestoreLastStatus();

        if (_sidePanePinned && AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_SIDE_PANE_OPENED, false))
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
                _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
            }
        });

        GetEventBus().With<string>(EventId.CloseTab).Observe(this, CloseTabNoLock);

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
            LoadTabNoLock(route, string.Empty);
        }
    }

    private bool LoadTabNoLock(Route route, string targetTabId, bool selectTab = true, string initiateTabId = "", string newTabId = "")
    {
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
                if (tab.CurrentBundle.Url == bundle.Url)
                {
                    if (selectTab)
                    {
                        RootTabView.SelectedItem = tab.Item;
                    }

                    return true;
                }
            }
        }

        bool newTab = string.IsNullOrEmpty(targetTabId);
        if (newTab)
        {
            targetTabId = AddTabNoLock(bundle, initiateTabId, newTabId);
        }

        TabInfo? tabInfo = GetTabInfoNoLock(targetTabId);
        if (tabInfo is null)
        {
            Logger.F(TAG, $"Failed to find tab info for ID {targetTabId}");
            return false;
        }

        if (selectTab)
        {
            RootTabView.SelectedItem = tabInfo.Item;
        }

        if (!newTab && tabInfo.CurrentBundle.Url == bundle.Url)
        {
            return true;
        }

        TransferAbility(bundle.Communicator, tabInfo);
        var frame = (Frame)tabInfo.Item.Content;
        frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        return true;
    }

    private string AddTabNoLock(NavigationBundle bundle, string initiateTabId, string newTabId)
    {
        int placementIndex = -1;
        if (!string.IsNullOrEmpty(initiateTabId))
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                TabInfo tab = _tabs[i];
                if (tab.Id == initiateTabId)
                {
                    placementIndex = i + 1;
                    break;
                }
            }
        }

        if (placementIndex == -1)
        {
            initiateTabId = string.Empty;
            placementIndex = _tabs.Count;
        }

        string tabId = newTabId;
        if (string.IsNullOrEmpty(tabId))
        {
            tabId = CreateNewTabId();
        }

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
            InitiateTabId = initiateTabId,
            Item = item,
            Ability = ability,
            NavigationBarAbility = navigationBarAbility,
            CurrentBundle = bundle,
        };
        tabInfo.NavigatedHandler = (sender, e) =>
        {
            var newBundle = (NavigationBundle)e.Parameter;
            tabInfo.NavigationBarAbility.ClearStates();
            tabInfo.CurrentBundle = newBundle;
            if (_currentTab is not null && tabInfo.Id == _currentTab.Id)
            {
                OnPageChanged();
            }
        };
        frame.Navigated += tabInfo.NavigatedHandler;

        _tabs.Insert(placementIndex, tabInfo);
        RootTabView.TabItems.Insert(placementIndex, tabInfo.Item);
        return tabId;
    }

    private void CloseTabNoLock(string tabId)
    {
        if (string.IsNullOrEmpty(tabId))
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

        if (closingTab is null)
        {
            Logger.F(TAG, $"Closing tab ID {tabId} not found");
            return;
        }

        CloseTabInternalNoLock(closingTab);

        if (_tabs.Count > 0)
        {
            if (!string.IsNullOrEmpty(closingTab.InitiateTabId))
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    TabInfo tab = _tabs[i];
                    if (tab.Id == closingTab.InitiateTabId)
                    {
                        RootTabView.SelectedIndex = i;
                    }
                }
            }
        }
        else
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

    private TabInfo? GetTabInfoNoLock(string tabId)
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
        LoadTabNoLock(route, string.Empty);
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        string closingTabId = string.Empty;
        for (int i = 0; i < _tabs.Count; ++i)
        {
            TabInfo tabInfo = _tabs[i];
            if (tabInfo.Item == args.Tab)
            {
                closingTabId = tabInfo.Id;
                break;
            }
        }

        if (_tabs.Count == 1)
        {
            switch (AppSettingsModel.Instance.CloseLastTabBehavior)
            {
                case AppSettingsModel.CloseLastTabBehaviorEnum.CloseWindow:
                    break;
                case AppSettingsModel.CloseLastTabBehaviorEnum.OpenHomePage:
                    {
                        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
                        LoadTabNoLock(route, string.Empty);
                    }
                    break;
                default:
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

        OnPageChanged();
    }

    private void RootTabView_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        // Reorder tabs to match the actual order
        var indexMap = RootTabView.TabItems
            .Select((value, index) => new { value, index })
            .ToDictionary(x => x.value, x => x.index);
        List<TabInfo> reordered = [.. _tabs
            .OrderBy(t => indexMap.TryGetValue(t.Item, out int idx) ? idx : int.MaxValue)];
        _tabs.Clear();
        foreach (TabInfo tab in reordered)
        {
            _tabs.Add(tab);
        }

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
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
        args.Data.Properties.Add("url", draggingTab.CurrentBundle.Url);
    }

    private void OnRootTabViewDrop(object sender, DragEventArgs e)
    {
        // Handle tab drag-drop
        if (e.DataView.Properties.TryGetValue("windowId", out object windowIdObj) && windowIdObj is int sourceWindowId &&
            e.DataView.Properties.TryGetValue("tabId", out object tabIdObj) && tabIdObj is string sourceTabId &&
            e.DataView.Properties.TryGetValue("url", out object urlObj) && urlObj is string url)
        {
            if (sourceWindowId != WindowId)
            {
                App.Instance.WindowManager.GetEventBus(sourceWindowId).With<string>(EventId.CloseTab).Emit(sourceTabId);
                LoadTabNoLock(Route.Create(url), string.Empty);
                EnsureInitialTabNoLock();
            }

            return;
        }

        // Handle file drop
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            CoroutineUtils.Run(async () =>
            {
                IReadOnlyList<Windows.Storage.IStorageItem> items = await e.DataView.GetStorageItemsAsync();
                foreach (Windows.Storage.IStorageItem? item in items)
                {
                    if (item is Windows.Storage.StorageFile file)
                    {
                        await App.Instance.OnCommandLine(CurrentWindow, [item.Path]);
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
        RootTabView.TabItems.Remove(removingTab.Item);
        MainWindow.Open(url: removingTab.CurrentBundle.Url);
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
        IPageTrait pageTrait = tabInfo.CurrentBundle.PageTrait;
        bool immersiveMode = pageTrait.ImmersiveMode();
        bool isHomePage = pageTrait is HomePageTrait;

        ViewModel.IsHomePage = isHomePage;
        ViewModel.CanGoBack = ((Frame)tabInfo.Item.Content).CanGoBack;
        ViewModel.CanGoForward = ((Frame)tabInfo.Item.Content).CanGoForward;

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

        GetEventBus().With<double>(EventId.TitleBarOpacity).Emit(_tabContainerGrid.Opacity);
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

        if (!show && !_currentTab.CurrentBundle.PageTrait.ImmersiveMode())
        {
            // Only hide the title bar when the current page supports immersive mode.
            return;
        }

        _titleBarVisible = show;
        double targetOpacity = show ? 1.0 : 0.0;

        _titleBarAnimation?.Stop();
        _titleBarAnimation = null;

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
            Logger.F(TAG, "Failed to remove ContentGrid from parent panel");
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
            Logger.F(TAG, "GetCurrentContentFrame: Current tab not set");
            return null;
        }

        return (Frame)tabInfo.Item.Content;
    }

    private NavigationPageAbility? GetCurrentNavigationBarAbility()
    {
        TabInfo? tabInfo = _currentTab;
        if (tabInfo is null)
        {
            Logger.F(TAG, "GetCurrentNavigationBarAbility: Current tab not set");
            return null;
        }

        return tabInfo.NavigationBarAbility;
    }

    private void SetCustomNavigationBar(UIElement? element)
    {
        CustomNavigationBarGrid.Children.Clear();

        if (element is not null)
        {
            CustomNavigationBarGrid.Children.Add(element);
        }
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
        double newWidth = NavigationPageSidePane.OpenPaneLength + NavigationPageSidePane.Margin.Right;
        if (_sidePaneWidth == newWidth)
        {
            return;
        }

        _sidePaneWidth = newWidth;
        DispatchRightOverlayWidthChangeEvent();
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_SIDE_PANE_WIDTH, newWidth);
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
            AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_SIDE_PANE_OPENED, opened);
        }
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
                handled = true;
                GetMainWindowAbility().ExitFullscreen();
                break;
            case Windows.System.VirtualKey.F10:
                if (ctrlDown)
                {
                    handled = true;
                    ViewModel.StartOrStopLogger();
                }
                break;
            case Windows.System.VirtualKey.F11:
                if (ctrlDown)
                {
                    handled = true;
                    ViewModel.ShowOrHideLogger();
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

    private static string CreateNewTabId()
    {
        return Guid.NewGuid().ToString();
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
        protected readonly WeakReference<MainPage> _parent = new(parent);
        private readonly LifecycleAwareAbility _lifecycleAbility = new();
        private readonly MutableLiveData<bool> _titleBarVisibilityChangeLiveData = new(parent._titleBarVisible);

        public abstract void OpenInCurrentTab(Route route);

        public abstract void OpenInNewTab(Route route);

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

        public void SetSidePanePage(SidePaneView.PageEnum page)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.RightSidePane.SetPage(page);
        }

        public LifecycleAwareAbility GetLifecycleAbility()
        {
            return _lifecycleAbility;
        }
    }

    private class MainPageAbilityForTab(MainPage parent, string tabId) : MainPageAbility(parent), IMainPageAbilityForTab
    {
        private readonly string _tabId = tabId;

        public string TabId => _tabId;

        public string Url
        {
            get
            {
                TabInfo? tab = GetTab();
                if (tab is null)
                {
                    return string.Empty;
                }

                return tab.CurrentBundle.Url;
            }
        }

        public override void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.LoadTabNoLock(route, _tabId, initiateTabId: _tabId);
        }

        public override void OpenInNewTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.LoadTabNoLock(route, string.Empty, initiateTabId: _tabId);
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

        public void SetUrl(string url)
        {
            TabInfo? tab = GetTab();
            if (tab == null)
            {
                return;
            }

            if (tab.CurrentBundle.Url == url)
            {
                return;
            }

            if (!IsSameSource(tab.CurrentBundle.Url, url))
            {
                Logger.F(TAG, $"Cannot set url to different source: {tab.CurrentBundle.Url} -> {url}");
                return;
            }

            tab.CurrentBundle.SetUrl(url);
            App.Instance.WindowManager.ScheduleSaveWindowStatus();
        }

        private TabInfo? GetTab()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return null;
            }

            return parent.GetTabInfoNoLock(_tabId);
        }

        private static bool IsSameSource(string url1, string url2)
        {
            if (!Uri.TryCreate(url1, UriKind.Absolute, out Uri? uri1))
            {
                return false;
            }

            if (!Uri.TryCreate(url2, UriKind.Absolute, out Uri? uri2))
            {
                return false;
            }

            return string.Equals(uri1.Scheme, uri2.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri1.Host, uri2.Host, StringComparison.OrdinalIgnoreCase)
                && uri1.Port == uri2.Port;
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

            string currentTabId = parent._currentTab?.Id ?? string.Empty;
            parent.LoadTabNoLock(route, tabInfo.Id, initiateTabId: currentTabId);
        }

        public override void OpenInNewTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            string currentTabId = parent._currentTab?.Id ?? string.Empty;
            parent.LoadTabNoLock(route, string.Empty, initiateTabId: currentTabId);
        }
    }

    private class NavigationPageAbility : INavigationPageAbility
    {
        private const string EVENT_REFRESH = "Refresh";

        private readonly WeakReference<MainPage> _parent;
        private WeakReference<UIElement>? _customNavigationBar;
        private readonly EventBus _eventBus = new();

        public NavigationPageAbility(MainPage parent)
        {
            _parent = new(parent);
            ClearStates();
        }

        public void ClearStates()
        {
            _eventBus.Clear();
        }

        public void RestoreStates()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            SetCustomNavigationBarInternal(parent);
        }

        //
        // Custom Navigation Bar
        //

        public void SetCustomNavigationBar(UIElement? element)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            _customNavigationBar = element is null ? null : new WeakReference<UIElement>(element);
            SetCustomNavigationBarInternal(parent);
        }

        private void SetCustomNavigationBarInternal(MainPage page)
        {
            if (_customNavigationBar is not null && _customNavigationBar.TryGetTarget(out UIElement? element))
            {
                page.SetCustomNavigationBar(element);
            }
            else
            {
                _customNavigationBar = null;
                page.SetCustomNavigationBar(null);
            }
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
    }

    //
    // Types
    //

    private class MainPageComponent(MainPage parent) : IMainPageComponent
    {
        protected readonly WeakReference<MainPage> _parent = new(parent);

        public string TabId
        {
            get
            {
                if (_parent.TryGetTarget(out MainPage? parent))
                {
                    TabInfo? currentTab = parent._currentTab;
                    if (currentTab is not null)
                    {
                        return currentTab.Id;
                    }
                }

                return string.Empty;
            }
        }
    }

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
        public required string Id { init; get; }
        public required string InitiateTabId { init; get; }
        public required TabViewItem Item { init; get; }
        public required MainPageAbilityForTab Ability { init; get; }
        public required NavigationPageAbility NavigationBarAbility { init; get; }
        public required NavigationBundle CurrentBundle { get; set; }
        public NavigatedEventHandler? NavigatedHandler { get; set; }

        //
        // ITabInfo Implmentation
        //

        string ITabInfo.Id => Id;

        string ITabInfo.Title => Item.Header as string ?? string.Empty;
    }

    public class LastTabStatusJsonModel
    {
        [JsonPropertyName("SelectedIndex")]
        public int SelectedIndex { get; set; }

        [JsonPropertyName("Tabs")]
        public List<TabJsonModel?>? Tabs { get; set; }

        public static LastTabStatusJsonModel FromUrl(string url)
        {
            return new LastTabStatusJsonModel
            {
                SelectedIndex = 0,
                Tabs =
                [
                    new()
                    {
                        Id = CreateNewTabId(),
                        Url = url,
                    }
                ]
            };
        }
    }

    public class TabJsonModel
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

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
        public required string Id { get; init; }
        public required string Url { get; set; }
    }

    public interface ITabInfo
    {
        string Id { get; }

        string Title { get; }
    }
}
