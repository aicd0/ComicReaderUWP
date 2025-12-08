// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.AppWindows.Main;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace ComicReader.Views.Pages.Main;

internal sealed partial class MainPage : BasePage
{
    private const string TAG = nameof(MainPage);

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

    private bool _titleBarVisible = true;
    private double _rootTabHeight = 0;
    private double _navigationBarHeight = 0;

    //
    // Properties
    //

    public string Title => _currentTab?.Item?.Header as string ?? string.Empty;

    private MainWindow? _currentWindow;
    private MainWindow CurrentWindow => _currentWindow!;

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
        MainThreadUtils.RunInMainThread(() =>
        {
            LoadTabNoLock(-1, route, true);
        });
    }

    public void OpenInCurrentTab(Route route)
    {
        MainThreadUtils.RunInMainThread(() =>
        {
            TabInfo? tab = _currentTab;
            if (tab is not null)
            {
                LoadTabNoLock(tab.Id, route, true);
            }
        });
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
    /// <returns></returns>
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

        MainThreadUtils.RunInMainThread(() =>
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
        ObserveData();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ViewModel.OnStop();
    }

    private void ObserveData()
    {
        OnscreenLogger.Started.ObserveSticky(this, ViewModel.SetLogStarted);
        OnscreenLogger.Visible.ObserveSticky(this, ViewModel.SetLogVisibility);

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
                FullscreenButtonGrid.Opacity = opacity;
                _tabContainerGrid.IsHitTestVisible = opacity > 0.5;
            }
        });

        GetEventBus().With<int>(EventId.CloseTab).Observe(this, CloseTabNoLock);

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, isFullscreen =>
        {
            ViewModel.IsFullscreen = isFullscreen;
        });
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

        route.WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
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

        var frame = (Frame)tabInfo.Item.Content;
        if (bundle.PageTrait.HasNavigationBar())
        {
            // Ensure that the frame has a NavigationPage as its content
            if (frame.Content is null || frame.Content.GetType() != typeof(NavigationPage))
            {
                Route navigationRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_NAVIGATION)
                    .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
                NavigationBundle? navigationPageBundle = AppRouter.Process(navigationRoute);
                if (navigationPageBundle is not null)
                {
                    TransferAbility(navigationPageBundle.Communicator, tabInfo.Ability);
                    if (!frame.Navigate(navigationPageBundle.PageTrait.GetPageType(), navigationPageBundle))
                    {
                        Logger.F(TAG, $"Failed to navigate to navigation page for tab ID {tabId}.");
                    }
                }
                else
                {
                    Logger.F(TAG, $"Failed to process navigation route: {navigationRoute.Url}");
                }
            }

            // If the frame's content is a NavigationPage, navigate to the new page
            if (frame.Content is not null && frame.Content.GetType() == typeof(NavigationPage))
            {
                var contentPage = (NavigationPage)frame.Content!;
                contentPage.Navigate(bundle);
            }
            else
            {
                Logger.F(TAG, $"Frame content is not a NavigationPage for tab ID {tabId}.");
            }
        }
        else
        {
            TransferAbility(bundle.Communicator, tabInfo.Ability);
            frame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        }

        return true;
    }

    private int AddTabNoLock(NavigationBundle bundle)
    {
        var item = new TabViewItem
        {
            Header = StringResource.Untitled,
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
        UpdateTopPadding();

        TabInfo? currentTab = _currentTab;
        if (currentTab != null)
        {
            IPageTrait pageTrait = currentTab.CurrentPageTrait;

            if (!pageTrait.ImmersiveMode())
            {
                ShowOrHideTitleBar(true, transitionAnimation: false);
            }

            if (pageTrait.HideFullscreenButton())
            {
                FullscreenButtonGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                FullscreenButtonGrid.Visibility = Visibility.Visible;
            }
        }

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
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
            if (!Started)
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
        _tabContentPresenter = sender as ContentPresenter;
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
    // Size Change Events
    //

    private void OnTabContainerGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        GetEventBus().With<double>(EventId.RootTabHeightChange).Emit(e.NewSize.Height);
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
        MainThreadUtils.RunInMainThread(() =>
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

    private void TransferAbility(PageCommunicator communicator, MainPageAbility ability)
    {
        ability.GetLifecycleAbility().Observe(this);
        communicator.RegisterAbility<ILifecycleAwareAbility>(ability);
        communicator.RegisterAbility(GetMainWindowAbility());
        communicator.RegisterAbility<IMainPageAbility>(ability);
    }

    private class MainPageAbility(MainPage parent, int tabId) : IMainPageAbility, ILifecycleAwareAbility
    {
        private const string EVENT_TAB_UNSELECTED = "TabUnselected";

        private readonly WeakReference<MainPage> _parent = new(parent);
        private readonly LifecycleAwareAbility _lifecycleAbility = new();
        private readonly EventBus _eventBus = new();
        private readonly MutableLiveData<bool> _titleBarVisibilityChangeLiveData = new(parent._titleBarVisible);
        private readonly int _tabId = tabId;

        public void OpenInCurrentTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.LoadTabNoLock(_tabId, route, true);
        }

        public void OpenInNewTab(Route route)
        {
            if (!_parent.TryGetTarget(out MainPage? parent))
            {
                return;
            }

            parent.OpenInNewTab(route);
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

            TabInfo? tab = parent.GetTabInfoNoLock(_tabId);
            if (tab == null)
            {
                return;
            }

            tab.CurrentPageTrait = pageTrait;
            tab.CurrentUrl = url;
            parent.OnPageChanged();
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

        public LifecycleAwareAbility GetLifecycleAbility()
        {
            return _lifecycleAbility;
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
}
