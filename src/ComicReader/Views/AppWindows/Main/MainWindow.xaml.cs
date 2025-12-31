// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

using ComicReader.Common.BaseUI;
using ComicReader.Common.InitTask;
using ComicReader.Common.Localization;
using ComicReader.Common.Services;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.Misc;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.Pages.Main;

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Win32;

using WinRT.Interop;

namespace ComicReader.Views.AppWindows.Main;

internal sealed partial class MainWindow : Window
{
    private const string TAG = nameof(MainWindow);
    private const uint WM_MOVE = 0x0003;

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

    //
    // Properties
    //

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }
    public bool Alive { get; private set; } = false;
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
        Members._requestRestorePlacement = restorePlacement;

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

    public void OpenTab(string url, int tabId)
    {
        var route = Route.Create(url);
        MainPage? mainPage = Members._mainPage;
        if (mainPage is null)
        {
            return;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            mainPage.Open(route, tabId);
        });
    }

    /// <summary>
    /// Must be called from the UI thread.
    /// </summary>
    /// <returns></returns>
    public WindowStatusModel? GetWindowStatus()
    {
        if (!Alive || Members._mainPage is null)
        {
            return null;
        }

        return new()
        {
            Fullscreen = Members._fullscreen,
            WindowPlacement = Members._windowPlacementManager.GetWindowPlacement(),
            TabStatus = Members._mainPage.GetTabStatus()
        };
    }

    public void BringToFront()
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
    }

    public void EnterFullscreen()
    {
        EnterOrExitFullscreen(true);
    }

    public void ExitFullscreen()
    {
        EnterOrExitFullscreen(false);
    }

    //
    // Event Handlers
    //

    // IMPORTANT: Handle event registration and unregistration in the code-behind file,
    // do not use XAML for this purpose in order to prevent memory leaks.

    private void SubscribeEvents()
    {
        Closed += OnWindowClosed;
        SizeChanged += OnWindowSizeChanged;
        PageFrame.Loaded += OnPageFrameLoaded;
        PageFrame.PointerEntered += OnPageFramePointerEntered;
        PageFrame.PointerExited += OnPageFramePointerExited;
    }

    private void UnsubscribeEvents()
    {
        Closed -= OnWindowClosed;
        SizeChanged -= OnWindowSizeChanged;
        PageFrame.Loaded -= OnPageFrameLoaded;
        PageFrame.PointerEntered -= OnPageFramePointerEntered;
        PageFrame.PointerExited -= OnPageFramePointerExited;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        bool isLastWindow = App.Instance.WindowManager.GetAllWindowInfo().Count == 1;
        if (isLastWindow)
        {
            ApplicationService.StartShuttingDown();
        }

        // Mark the end of the window lifecycle
        Alive = false;

        // Close all tabs and dispatch page stopped event
        Members._mainPage!.CloseAllTabs();
        Members._mainWindowAbility.GetLifecycleAbility().SetCustomState("Window", ILifecycle.State.Stopped);

        // Unsubscribe window events
        UnsubscribeEvents();

        // Unregister message loop
        UnregisterMessageLoop();

        // Unregister window from WindowManager
        App.Instance.WindowManager.UnregisterWindow(WindowId);

        // Dereference all members
        _members = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        CoroutineUtils.PostInMainThread(() =>
        {
            DispatchFullscreenChangeEvent(IsFullScreen());
        });

        App.Instance.WindowManager.ScheduleSaveWindowStatus();
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        // Load the main page
        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN);
        NavigationBundle bundle = AppRouter.Process(route)!;
        bundle.Communicator.RegisterAbility<ILifecycleAwareAbility>(Members._mainWindowAbility);
        bundle.Communicator.RegisterAbility<IMainWindowAbility>(Members._mainWindowAbility);
        PageFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        Members._mainPage = (MainPage)PageFrame.Content;

        // Mark the beginning of the window lifecycle
        Alive = true;

        // Restore window placement
        WindowStatusModel? windowStatus = Members._requestWindowStatus;
        if (windowStatus is not null && Members._requestRestorePlacement)
        {
            if (windowStatus.Fullscreen)
            {
                EnterOrExitFullscreen(true);
            }
            else if (windowStatus.WindowPlacement is not null)
            {
                Members._windowPlacementManager.RestoreWindowPlacement(windowStatus.WindowPlacement);
            }
        }

        // Load tabs
        Members._mainPage.RestoreTabStatus(windowStatus?.TabStatus);

        if (WindowMembers.sIsFirstWindow)
        {
            WindowMembers.sIsFirstWindow = false;

            // Show last crash report if applicable
            if (!App.Instance.ExitedNormallyLastTime && DebugUtils.DebugMode)
            {
                DebugUtils.ReportLastCrash();
            }

            if (AppSettingsModel.Instance.GetModel().ScanOnLaunch)
            {
                // Update comic library
                // We delay this operation to here because it might involve dialog display which requires an active window
                ComicModel.UpdateAllComics("InitOnAppLaunchInternal");
            }
        }

        LaunchPerformanceTracker.MarkTabRestored();
    }

    private void OnPageFramePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Members._pointerInWindow = true;
    }

    private void OnPageFramePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Members._pointerInWindow = false;
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
            Logger.F(TAG, "Failed to register message loop.");
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
                Logger.F(TAG, "Failed to unregister message loop.");
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
            case WM_MOVE:
                App.Instance.WindowManager.ScheduleSaveWindowStatus();
                break;
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
        if (Members._fullscreen == isFullscreen)
        {
            return;
        }

        Members._fullscreen = isFullscreen;
        Members._mainWindowAbility.SendFullscreenChangedEvent(isFullscreen);
    }

    //
    // Helpers
    //

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
        private readonly MutableLiveData<bool> _fullscreenChangeLiveData = new(false);

        public MainWindowAbility(MainWindow window)
        {
            _windowId = window.WindowId;
            _windowRef = new(window);
        }

        public int WindowId => _windowId;

        public bool PointerInWindow()
        {
            return GetWindow()?.Members?._pointerInWindow ?? false;
        }

        public void EnterFullscreen()
        {
            GetWindow()?.EnterOrExitFullscreen(true);
        }

        public void ExitFullscreen()
        {
            GetWindow()?.EnterOrExitFullscreen(false);
        }

        public void RegisterFullscreenChangedHandler(ILifecycleOwner owner, IMainWindowAbility.FullscreenChangedEventHandler handler)
        {
            _fullscreenChangeLiveData.ObserveSticky(owner, delegate (bool isFullscreen)
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
        public static bool sIsFirstWindow = true;

        public MainPage? _mainPage;
        public WindowStatusModel? _requestWindowStatus = null;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _originProc;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _wndProcDelegate;
        public readonly MainWindowAbility _mainWindowAbility = new(window);
        public bool _fullscreen = false;
        public bool _requestRestorePlacement = false;
        public bool _pointerInWindow = false;
        public WindowPlacementManager _windowPlacementManager = new(window);
    }

    public class WindowStatusModel
    {
        [JsonPropertyName("Fullscreen")]
        public bool Fullscreen { get; init; }

        [JsonPropertyName("WindowPlacement")]
        public WindowPlacementManager.SavedWindowState? WindowPlacement { get; init; }

        [JsonPropertyName("TabStatus")]
        public required MainPage.LastTabStatusJsonModel TabStatus { get; init; }

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
