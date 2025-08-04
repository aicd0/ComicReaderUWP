// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.ApplicationModel.DataTransfer;

namespace ComicReader.Views.Pages.Main;

internal sealed partial class MainPage : BasePage
{
    public MainPageViewModel ViewModel { get; } = new();

    //
    // Member variables
    //

    private Grid? _tabContainerGrid;
    private ContentPresenter? _tabContentPresenter;
    private Storyboard? _titleBarAnimation;
    private long _tabContainerGridOpacityListenerToken = 0;

    private readonly List<TabInfo> _tabs = [];
    private TabInfo? _currentTab;
    private int _nextTabId = 0;
    private bool _isFullscreen = false;

    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;

    //
    // Properties
    //

    private Window? CurrentWindow => App.WindowManager.GetWindow(WindowId);

    //
    // Constructors
    //

    public MainPage()
    {
        InitializeComponent();
    }

    //
    // Public Methods
    //

    public void OpenInNewTab(Route route)
    {
        LoadTab(-1, route);
    }

    public void CloseAllTabs()
    {
        while (_tabs.Count > 0)
        {
            CloseTab(_tabs[0]);
        }
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        Window window = CurrentWindow!;
        window.SetTitleBar(MainTitleBar);

        AppWindowTitleBar titleBar = window.AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = MainTitleBar.ButtonBackground?.Color;
        titleBar.ButtonForegroundColor = MainTitleBar.ButtonForeground?.Color;
        titleBar.ButtonInactiveBackgroundColor = MainTitleBar.ButtonInactiveBackground?.Color;
        titleBar.ButtonInactiveForegroundColor = MainTitleBar.ButtonInactiveForeground?.Color;
        titleBar.ButtonHoverBackgroundColor = MainTitleBar.ButtonHoverBackground?.Color;
        titleBar.ButtonHoverForegroundColor = MainTitleBar.ButtonHoverForeground?.Color;
        titleBar.ButtonPressedBackgroundColor = MainTitleBar.ButtonPressedBackground?.Color;
        titleBar.ButtonPressedForegroundColor = MainTitleBar.ButtonPressedForeground?.Color;

        ViewModel.OnStart();

        string url = bundle.GetString(RouterConstants.ARG_URL);
        _ = OnFirstStartUp(url);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ViewModel.OnStop();
    }

    private async Task OnFirstStartUp(string url)
    {
        if (url != null && url.Length > 0)
        {
            Route route = Route.Create(url).WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
            LoadTab(-1, route);
            return;
        }

        {
            long id = AppModel.GetReadingComic();
            if (id >= 0)
            {
                ComicModel? comic = await ComicModel.FromId(id, "FetchLastComic");
                if (comic != null)
                {
                    Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    OpenInNewTab(route);
                    return;
                }
            }
        }

        {
            var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
            OpenInNewTab(route);
        }
    }

    private void ObserveData()
    {
        GetEventBus().With<double>(EventId.RootTabHeightChange).ObserveSticky(this, delegate (double h)
        {
            _rootTabHeight = h;
            GetEventBus().With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
            UpdateTopPadding();
        });

        GetEventBus().With<double>(EventId.NavigationBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            _navigationBarHeight = h;
            GetEventBus().With<double>(EventId.TitleBarHeightChange).Emit(_rootTabHeight + _navigationBarHeight);
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            if (_tabContainerGrid != null)
            {
                _tabContainerGrid.Opacity = opacity;
                _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
            }
        });

        GetEventBus().With<int>(EventId.CloseTab).Observe(this, CloseTab);

        GetEventBus().With(EventId.HotKeyF11).Observe(this, delegate
        {
            ViewModel.NextLogType();
        });
    }

    //
    // Tab Management
    //

    private int AddTab(NavigationBundle bundle)
    {
        var item = new TabViewItem
        {
            Header = "Loading...",
            Content = new Frame()
        };

        int tabId = _nextTabId++;
        var ability = new MainPageAbility(this, tabId);
        var tabInfo = new TabInfo(tabId, item)
        {
            CurrentPageTrait = bundle.PageTrait,
            CurrentUrl = bundle.Url,
            Ability = ability
        };

        RegisterPageAbility(bundle.Communicator, ability);
        _tabs.Add(tabInfo);
        RootTabView.TabItems.Add(item);
        return tabId;
    }

    private void LoadTab(int tabId, Route route)
    {
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            LoadTabInternal(tabId, route);
        });
    }

    private void LoadTabInternal(int tabId, Route route)
    {
        if (tabId < -1)
        {
            throw new ArgumentException();
        }

        route.WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
        NavigationBundle bundle = AppRouter.Process(route);

        if (!bundle.PageTrait.SupportMultiInstance())
        {
            foreach (TabInfo tab in _tabs)
            {
                if (tab.CurrentUrl == bundle.Url)
                {
                    RootTabView.SelectedItem = tab.Item;
                    return;
                }
            }
        }

        bool newTab = tabId == -1;

        if (newTab)
        {
            tabId = AddTab(bundle);
        }

        TabInfo? tabInfo = GetTabInfo(tabId);

        if (tabInfo == null)
        {
            Logger.AssertNotReachHere("CF1E732FD7F4EECA");
            return;
        }

        RootTabView.SelectedItem = tabInfo.Item;

        if (!newTab && tabInfo.CurrentUrl == bundle.Url)
        {
            return;
        }

        var frame = (Frame)tabInfo.Item.Content;

        if (bundle.PageTrait.HasNavigationBar())
        {
            if (frame.Content == null || frame.Content.GetType() != typeof(NavigationPage))
            {
                Route navigationRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_NAVIGATION)
                    .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
                NavigationBundle navigationPageBundle = AppRouter.Process(navigationRoute);
                RegisterPageAbility(navigationPageBundle.Communicator, tabInfo.Ability);
                if (!frame.Navigate(navigationPageBundle.PageTrait.GetPageType(), navigationPageBundle))
                {
                    return;
                }
            }

            var contentPage = (NavigationPage)frame.Content!;
            contentPage.Navigate(bundle);
        }
        else
        {
            frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        }

        OnPageChanged();
    }

    private void CloseTab(int tabId)
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

        CloseTab(closingTab);

        if (RootTabView.TabItems.Count <= 0)
        {
            CurrentWindow?.Close();
        }
    }

    private void CloseTab(TabInfo tabInfo)
    {
        tabInfo.Ability.DispatchPageStoppedEvent();
        _tabs.Remove(tabInfo);
        RootTabView.TabItems.Remove(tabInfo.Item);
    }

    private TabInfo? GetTabInfo(int tabId)
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
        OpenInNewTab(route);
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

        CloseTab(closingTabId);
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

        if (lastSelectedTab != null)
        {
            DispatchToTab(lastSelectedTab, delegate (MainPageAbility ability)
            {
                ability.SendTabUnselectedEvent();
            });
        }

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
        int sourceWindowId;
        {
            if (!e.DataView.Properties.TryGetValue("windowId", out object id) || id is not int)
            {
                Logger.AssertNotReachHere("98CC0674EF182B5D");
                return;
            }
            sourceWindowId = (int)id;
        }
        int sourceTabId;
        {
            if (!e.DataView.Properties.TryGetValue("tabId", out object id) || id is not int)
            {
                Logger.AssertNotReachHere("352E7E7D7070988A");
                return;
            }
            sourceTabId = (int)id;
        }
        string url;
        {
            if (!e.DataView.Properties.TryGetValue("url", out object u) || u is not string)
            {
                Logger.AssertNotReachHere("E6337F0738EFC223");
                return;
            }
            url = (string)u;
        }

        if (sourceWindowId == WindowId)
        {
            return;
        }

        LoadTab(-1, Route.Create(url));
        App.WindowManager.GetEventBus(sourceWindowId).With<int>(EventId.CloseTab).Emit(sourceTabId);
    }

    private void OnRootTabViewDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
    }

    private void OnRootTabViewTabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
    {
        TabViewItem tab = args.Tab;
        Logger.Assert(tab != null, "556A8735ED29D6B5");

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

        var newWindow = new MainWindow(removingTab.CurrentUrl);
        newWindow.Activate();
    }

    private void OnPageChanged()
    {
        UpdateTopPadding();

        TabInfo? currentTab = _currentTab;
        if (currentTab != null)
        {
            IPageTrait pageTrait = currentTab.CurrentPageTrait;

            if (!pageTrait.ImmersiveMode())
            {
                _titleBarAnimation?.Stop();
                GetEventBus().With<double>(EventId.TitleBarOpacity).Emit(1.0);
            }

            if (!pageTrait.SupportFullscreen())
            {
                ExitFullscreen();
            }
        }
    }

    private void UpdateTopPadding()
    {
        if (_currentTab == null || _tabContentPresenter == null)
        {
            return;
        }

        if (_currentTab.CurrentPageTrait.ImmersiveMode())
        {
            _tabContentPresenter.Margin = new Thickness(0, 0, 0, 0);
            RootTabView.Background = (Brush)Application.Current.Resources["TitleBarBackground"];
        }
        else
        {
            _tabContentPresenter.Margin = new Thickness(0, _rootTabHeight, 0, 0);
            RootTabView.Background = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void OnTabContainerGridLoaded(object sender, RoutedEventArgs e)
    {
        _tabContainerGrid = (Grid)sender;

        _tabContainerGridOpacityListenerToken = _tabContainerGrid.RegisterPropertyChangedCallback(OpacityProperty, (sender, dp) =>
        {
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
        _tabContentPresenter = sender as ContentPresenter;
    }

    //
    // Title Bar Animation
    //

    private void ShowOrHideTitleBar(bool show)
    {
        if (_currentTab == null || !_currentTab.CurrentPageTrait.ImmersiveMode())
        {
            return;
        }

        UIElement? targetElement = _tabContainerGrid;
        if (targetElement == null)
        {
            return;
        }

        if (_titleBarAnimation != null)
        {
            _titleBarAnimation.Stop();
            _titleBarAnimation = null;
        }

        DoubleAnimation animation = new()
        {
            From = targetElement.Opacity,
            To = show ? 1.0 : 0.0,
            Duration = TimeSpan.FromSeconds(0.2),
        };

        Storyboard.SetTarget(animation, targetElement);
        Storyboard.SetTargetProperty(animation, "Opacity");
        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _titleBarAnimation = storyboard;
    }

    //
    // Fullscreen
    //

    private void EnterFullscreen()
    {
        Window? window = CurrentWindow;
        if (window == null || IsFullScreen(window))
        {
            return;
        }

        window.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        DispatchFullscreenChangeEvent(true);
    }

    private void ExitFullscreen()
    {
        Window? window = CurrentWindow;
        if (window == null || !IsFullScreen(window))
        {
            return;
        }

        window.AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        DispatchFullscreenChangeEvent(false);
    }

    private void DispatchFullscreenChangeEvent(bool isFullscreen)
    {
        if (_isFullscreen == isFullscreen)
        {
            return;
        }
        _isFullscreen = isFullscreen;

        DispatchToAllTabs(delegate (MainPageAbility ability)
        {
            ability.SendFullscreenChangedEvent(isFullscreen);
        });
    }

    private bool IsFullScreen(Window window)
    {
        return window.AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
    }

    //
    // Size Change Events
    //

    private void OnTabContainerGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        GetEventBus().With<double>(EventId.RootTabHeightChange).Emit(e.NewSize.Height);
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Window? window = CurrentWindow;
        if (window != null && !IsFullScreen(window))
        {
            DispatchFullscreenChangeEvent(false);
        }
    }

    //
    // Key Events
    //

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                ExitFullscreen();
                handled = true;
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    //
    // Utilities
    //

    private void DispatchToTab(TabInfo tab, Action<MainPageAbility> action)
    {
        action(tab.Ability);
    }

    private void DispatchToAllTabs(Action<MainPageAbility> action)
    {
        foreach (TabInfo tab in _tabs)
        {
            DispatchToTab(tab, action);
        }
    }

    //
    // Page Ability
    //

    private static void RegisterPageAbility(PageCommunicator communicator, MainPageAbility ability)
    {
        communicator.RegisterAbility<ICommonPageAbility>(ability);
        communicator.RegisterAbility<IMainPageAbility>(ability);
    }

    private class MainPageAbility(MainPage parent, int tabId) : ICommonPageAbility, IMainPageAbility
    {
        private const string EVENT_TAB_UNSELECTED = "TabUnselected";
        private const string EVENT_FULLSCREEN_CHANGED = "FullscreenChanged";

        private readonly WeakReference<MainPage> _parent = new(parent);
        private readonly EventBus _eventBus = new();
        private readonly int _tabId = tabId;

        private PageStopEventHandler? _pageStopped;

        public void RegisterPageStopHandler(PageStopEventHandler handler)
        {
            _pageStopped += handler;
        }

        public void UnregisterPageStopHandler(PageStopEventHandler handler)
        {
            _pageStopped -= handler;
        }

        public void DispatchPageStoppedEvent()
        {
            _pageStopped?.Invoke();
            _pageStopped = null;
        }

        public void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.LoadTab(_tabId, route);
        }

        public void OpenInNewTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.OpenInNewTab(route);
        }

        public void EnterFullscreen()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.EnterFullscreen();
        }

        public void ExitFullscreen()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.ExitFullscreen();
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

        public void SetCurrentPageInfo(string url, IPageTrait pageTrait)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            TabInfo? tab = parent.GetTabInfo(_tabId);
            if (tab == null)
            {
                return;
            }

            tab.CurrentPageTrait = pageTrait;
            tab.CurrentUrl = url;
            parent.OnPageChanged();
        }

        public void RegisterTabUnselectedHandler(Page owner, IMainPageAbility.TabUnselectedEventHandler handler)
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

        public void RegisterFullscreenChangedHandler(Page owner, IMainPageAbility.FullscreenChangedEventHandler handler)
        {
            _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).ObserveSticky(owner, delegate (bool isFullscreen)
            {
                handler(isFullscreen);
            });
        }

        public void ShowOrHideTitleBar(bool show)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.ShowOrHideTitleBar(show);
        }

        public void SendFullscreenChangedEvent(bool isFullscreen)
        {
            _eventBus.With<bool>(EVENT_FULLSCREEN_CHANGED).Emit(isFullscreen);
        }

        private TabInfo? GetTab()
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return null;
            }

            return parent.GetTabInfo(_tabId);
        }
    }

    //
    // Types
    //

    private class TabInfo(int id, TabViewItem item)
    {
        public TabViewItem Item { get; } = item;
        public int Id { get; } = id;
        public required MainPageAbility Ability { get; set; }
        public required string CurrentUrl { get; set; }
        public required IPageTrait CurrentPageTrait { get; set; }
    }
}
