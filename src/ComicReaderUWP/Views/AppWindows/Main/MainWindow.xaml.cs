// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.InitTask;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Views.Pages.Main;

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Win32;

using WinRT.Interop;

namespace ComicReaderUWP.Views.AppWindows.Main;

internal sealed partial class MainWindow : Window
{
    private const string TAG = nameof(MainWindow);

    private static bool sIsFirstWindow = true;

    //
    // Creators
    //

    public static void Open()
    {
        MainWindow window = new(null, restorePlacement: false);
        window.Activate();
    }

    public static void Open(string url, bool restorePlacement = false)
    {
        MainWindow window = new(WindowStatusModel.FromUrl(url), restorePlacement);
        window.Activate();
    }

    public static void Open(WindowStatusModel windowStatus)
    {
        MainWindow window = new(windowStatus, restorePlacement: true);
        window.Activate();
    }

    //
    // Member variables
    //

    // IMPORTANT: Any object referenced by this class need be dereferenced in OnWindowClosed,
    // or else memory leaks will occur.
    // See http://github.com/microsoft/microsoft-ui-xaml/issues/7282 for more details.

    private WindowMembers? _members;
    private WindowMembers Members => _members!;

    private readonly bool _requestRestorePlacement = false;
    private bool _minimized = false;
    private bool _fullscreen = false;
    private bool _pointerInWindow = false;

    //
    // Properties
    //

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }
    public WindowLifecycleState LifecycleState { get; private set; } = WindowLifecycleState.Initialized;
    public bool IsActive => PInvoke.GetActiveWindow() == new Windows.Win32.Foundation.HWND(WindowHandle);
    public MainPage.ITabInfo? CurrentTab => Members._mainPage?.CurrentTab;

    //
    // Constructors
    //

    private MainWindow(WindowStatusModel? windowStatus, bool restorePlacement)
    {
        InitializeComponent();

        WindowId = App.Instance.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        _members = new(this);
        Members._requestWindowStatus = windowStatus;
        _requestRestorePlacement = restorePlacement;

        if (DebugUtils.DeveloperMode)
        {
            RegisterMessageLoop();
        }

        Title = StringResourceProvider.Instance.AppDisplayName;
        ExtendsContentIntoTitleBar = true;
        TrySetAcrylicBackdrop();
        SetWindowIcon();
        SubscribeEvents();
    }

    //
    // Public Methods
    //

    public void OpenTab(string url, string targetTabId, string initiateTabId)
    {
        MainThreadUtils.AssertOnMainThread();
        Enqueue(() =>
        {
            MainPage? mainPage = Members._mainPage;
            if (mainPage is null)
            {
                return;
            }

            var route = Route.Create(url);
            mainPage.Open(route, targetTabId, initiateTabId);
        });
    }

    public void BringToFront()
    {
        MainThreadUtils.AssertOnMainThread();
        Enqueue(() =>
        {
            var hWnd = new Windows.Win32.Foundation.HWND(WindowHandle);

            Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT placement;
            unsafe
            {
                placement = new()
                {
                    length = (uint)sizeof(Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT)
                };
            }

            bool gotPlacement = PInvoke.GetWindowPlacement(hWnd, ref placement);
            if (!gotPlacement || placement.showCmd == Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
            {
                PInvoke.ShowWindow(hWnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_RESTORE);
            }

            PInvoke.SetForegroundWindow(hWnd);
        });
    }

    public void EnterFullscreen()
    {
        MainThreadUtils.AssertOnMainThread();
        EnterOrExitFullscreen(true);
    }

    public void ExitFullscreen()
    {
        MainThreadUtils.AssertOnMainThread();
        EnterOrExitFullscreen(false);
    }

    public WindowStatusModel? GetWindowStatus()
    {
        MainThreadUtils.AssertOnMainThread();

        if (LifecycleState != WindowLifecycleState.Loaded || Members._mainPage is null)
        {
            return null;
        }

        MainPage.LastTabStatusJsonModel? tabStatus = Members._mainPage.GetTabStatus();
        if (tabStatus is null)
        {
            return null;
        }

        return new()
        {
            Fullscreen = _fullscreen,
            WindowPlacement = Members._windowPlacementManager.GetWindowPlacement(),
            TabStatus = tabStatus,
        };
    }

    //
    // Event Handlers
    //

    // IMPORTANT: Handle event registration and unregistration in the code-behind file,
    // do not use XAML for this purpose in order to prevent memory leaks.

    private void SubscribeEvents()
    {
        Closed += OnWindowClosed;
        AppWindow.Changed += AppWindow_Changed;
        PageFrame.Loaded += OnPageFrameLoaded;
        PageFrame.PointerEntered += OnPageFramePointerEntered;
        PageFrame.PointerExited += OnPageFramePointerExited;
    }

    private void UnsubscribeEvents()
    {
        Closed -= OnWindowClosed;
        AppWindow.Changed -= AppWindow_Changed;
        PageFrame.Loaded -= OnPageFrameLoaded;
        PageFrame.PointerEntered -= OnPageFramePointerEntered;
        PageFrame.PointerExited -= OnPageFramePointerExited;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (App.Instance.WindowManager.GetAllWindowInfo().Count == 1)
        {
            ApplicationService.StartExiting();
        }

        LifecycleState = WindowLifecycleState.Destroyed;

        // Close all tabs and dispatch stop events
        Members._mainPage!.CloseAllTabs();
        Members._mainWindowAbility.GetLifecycleAbility().SetCustomState("Window", ILifecycle.State.Stopped);

        UnsubscribeEvents();
        UnregisterMessageLoop();
        App.Instance.WindowManager.UnregisterWindow(WindowId);
        App.Instance.WindowManager.ScheduleSaveWindowStatus();

        // Dereference members
        _members = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (LifecycleState != WindowLifecycleState.Loaded)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter presenter)
        {
            bool minimized = presenter.State == OverlappedPresenterState.Minimized;
            if (minimized != _minimized)
            {
                _minimized = minimized;
                Members._mainWindowAbility.SendMinimizeChangedEvent(minimized);
            }
        }

        if (args.DidPositionChange)
        {
            App.Instance.WindowManager.ScheduleSaveWindowStatus();
        }

        if (args.DidSizeChange)
        {
            CoroutineUtils.PostInMainThread(() =>
            {
                DispatchFullscreenChangeEvent(IsFullScreen());
            });

            App.Instance.WindowManager.ScheduleSaveWindowStatus();
        }
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        // Load the main page
        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN);
        PageNavigationBundle bundle = AppRouter.Process(route)!;
        bundle.Communicator.RegisterAbility<ILifecycleAwareAbility>(Members._mainWindowAbility);
        bundle.Communicator.RegisterAbility<IMainWindowAbility>(Members._mainWindowAbility);
        PageFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        Members._mainPage = (MainPage)PageFrame.Content;

        LifecycleState = WindowLifecycleState.Loaded;

        // Restore window placement
        WindowStatusModel? windowStatus = Members._requestWindowStatus;
        if (windowStatus is not null && _requestRestorePlacement)
        {
            if (windowStatus.WindowPlacement is not null)
            {
                Members._windowPlacementManager.RestoreWindowPlacement(windowStatus.WindowPlacement);
            }

            if (windowStatus.Fullscreen)
            {
                EnterOrExitFullscreen(true);
            }
        }

        // Load initial tabs
        Members._mainPage.RestoreTabStatus(windowStatus?.TabStatus);

        if (sIsFirstWindow)
        {
            sIsFirstWindow = false;

            // Show crash report if applicable
            if (!InitTaskManager.Instance.ExitedNormallyLastTime)
            {
                DebugUtils.ReportLastCrash();
            }

            if (AppSettingsModel.Instance.GetModel().ScanOnLaunch)
            {
                // Update comic library
                // This operation is deferred to here because it may involve dialog displaying which requires a loaded window
                ComicModel.UpdateAllComics("InitOnAppLaunchInternal");
            }
        }

        DequeuePendingActions();

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(5000);
            ApplicationService.StopLaunching();
        });
    }

    private void OnPageFramePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _pointerInWindow = true;
    }

    private void OnPageFramePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _pointerInWindow = false;
    }

    //
    // Win32 Message Loop
    //

    private void RegisterMessageLoop()
    {
        Windows.Win32.Foundation.HWND hwnd = new(WindowHandle);
        var wndProcDelegate = new Windows.Win32.UI.WindowsAndMessaging.WNDPROC(MessageLoopProc);
        Members._wndProcDelegate = wndProcDelegate;
        nint wndPrcPointer = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
#if x86
        nint prevWndProc = PInvoke.SetWindowLong(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, (int)wndPrcPointer);
#elif x64 || ARM64
        nint prevWndProc = PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, wndPrcPointer);
#endif
        if (prevWndProc == IntPtr.Zero)
        {
            Logger.F(TAG, "Failed to register message loop");
            return;
        }

        Members._originProc = Marshal.GetDelegateForFunctionPointer<Windows.Win32.UI.WindowsAndMessaging.WNDPROC>(prevWndProc);
    }

    private void UnregisterMessageLoop()
    {
        if (Members._originProc != null && Members._wndProcDelegate != null && WindowHandle != IntPtr.Zero)
        {
            Windows.Win32.Foundation.HWND hwnd = new(WindowHandle);
            nint originProcPtr = Marshal.GetFunctionPointerForDelegate(Members._originProc);
#if x86
            nint prevWndProc = PInvoke.SetWindowLong(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, (int)originProcPtr);
#elif x64 || ARM64
            nint prevWndProc = PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, originProcPtr);
#endif
            if (prevWndProc == IntPtr.Zero)
            {
                Logger.F(TAG, "Failed to unregister message loop");
            }
        }

        Members._originProc = null;
        Members._wndProcDelegate = null;
    }

    private Windows.Win32.Foundation.LRESULT MessageLoopProc(Windows.Win32.Foundation.HWND hwnd,
        uint uMsg,
        Windows.Win32.Foundation.WPARAM wParam,
        Windows.Win32.Foundation.LPARAM lParam)
    {
        switch (uMsg)
        {
            default:
                break;
        }

        return PInvoke.CallWindowProc(Members._originProc, hwnd, uMsg, wParam, lParam);
    }

    //
    // Fullscreen
    //

    private void EnterOrExitFullscreen(bool isFullscreen)
    {
        if (IsFullScreen() == isFullscreen)
        {
            return;
        }

        if (isFullscreen)
        {
            Members._windowPlacementManager.FreezeWindowPlacement();
        }
        else
        {
            Members._windowPlacementManager.UnfreezeWindowPlacement();
        }

        AppWindow.SetPresenter(isFullscreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
        DispatchFullscreenChangeEvent(isFullscreen);
        App.Instance.WindowManager.ScheduleSaveWindowStatus();
    }

    private bool IsFullScreen()
    {
        return AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
    }

    private void DispatchFullscreenChangeEvent(bool isFullscreen)
    {
        if (_fullscreen == isFullscreen)
        {
            return;
        }

        _fullscreen = isFullscreen;
        Members._mainWindowAbility.SendFullscreenChangedEvent(isFullscreen);
    }

    //
    // Helpers
    //

    private void Enqueue(Action action)
    {
        switch (LifecycleState)
        {
            case WindowLifecycleState.Initialized:
                Members._pendingActions.Add(action);
                break;
            case WindowLifecycleState.Loaded:
                action();
                break;
            case WindowLifecycleState.Destroyed:
                break;
            default:
                break;
        }
    }

    private void DequeuePendingActions()
    {
        List<Action> pendingActions = [.. Members._pendingActions];
        Members._pendingActions.Clear();
        foreach (Action action in pendingActions)
        {
            action();
        }
    }

    private void TrySetAcrylicBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            var desktopAcrylicBackdrop = new DesktopAcrylicBackdrop();
            SystemBackdrop = desktopAcrylicBackdrop;
        }
    }

    private void SetWindowIcon()
    {
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(@"Assets\AppIcon.ico");
    }

    //
    // Page Ability
    //

    private class MainWindowAbility : IMainWindowAbility, ILifecycleAwareAbility
    {
        private readonly int _windowId;
        private readonly WeakReference<MainWindow> _windowRef;
        private readonly LifecycleAwareAbility _lifecycleAbility = new();
        private readonly MutableLiveData<bool> _minimizeChangeLiveData = new(false);
        private readonly MutableLiveData<bool> _fullscreenChangeLiveData = new(false);

        public MainWindowAbility(MainWindow window)
        {
            _windowId = window.WindowId;
            _windowRef = new(window);
        }

        public int WindowId => _windowId;

        public bool IsActive => GetWindow()?.IsActive ?? false;

        public bool IsMinimized => _minimizeChangeLiveData.GetValue();

        public bool IsFullscreen => _fullscreenChangeLiveData.GetValue();

        public bool PointerInWindow()
        {
            return GetWindow()?._pointerInWindow ?? false;
        }

        public void EnterFullscreen()
        {
            GetWindow()?.EnterOrExitFullscreen(true);
        }

        public void ExitFullscreen()
        {
            GetWindow()?.EnterOrExitFullscreen(false);
        }

        public void RegisterMinimizeChangedHandler(ILifecycleOwner owner, IMainWindowAbility.MinimizeChangedEventHandler handler)
        {
            _minimizeChangeLiveData.ObserveSticky(owner, isMinimized =>
            {
                handler(isMinimized);
            });
        }

        public void SendMinimizeChangedEvent(bool isMinimized)
        {
            _minimizeChangeLiveData.Emit(isMinimized);
        }

        public void RegisterFullscreenChangedHandler(ILifecycleOwner owner, IMainWindowAbility.FullscreenChangedEventHandler handler)
        {
            _fullscreenChangeLiveData.ObserveSticky(owner, isFullscreen =>
            {
                handler(isFullscreen);
            });
        }

        public void SendFullscreenChangedEvent(bool isFullscreen)
        {
            _fullscreenChangeLiveData.Emit(isFullscreen);
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

        private MainWindow? GetWindow()
        {
            if (_windowRef.TryGetTarget(out MainWindow? window))
            {
                return window;
            }

            return null;
        }
    }

    //
    // Types
    //

    private class WindowMembers(MainWindow window)
    {
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _originProc;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _wndProcDelegate;
        public readonly WindowPlacementManager _windowPlacementManager = new(window);
        public readonly MainWindowAbility _mainWindowAbility = new(window);
        public List<Action> _pendingActions = [];
        public WindowStatusModel? _requestWindowStatus = null;
        public MainPage? _mainPage;
    }

    public class WindowStatusModel
    {
        [JsonPropertyName("Fullscreen")]
        public bool Fullscreen { get; init; }

        [JsonPropertyName("WindowPlacement")]
        public WindowPlacementManager.SavedWindowState? WindowPlacement { get; init; }

        [JsonPropertyName("TabStatus")]
        public MainPage.LastTabStatusJsonModel? TabStatus { get; init; }

        public static WindowStatusModel FromUrl(string url)
        {
            return new WindowStatusModel
            {
                Fullscreen = false,
                WindowPlacement = null,
                TabStatus = MainPage.LastTabStatusJsonModel.FromUrl(url)
            };
        }
    }
}
