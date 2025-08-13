// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.Native;
using ComicReader.Views.Pages.Main;

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Storage;
using Windows.Win32;

using WinRT.Interop;

namespace ComicReader;

public sealed partial class MainWindow : Window
{
    private const uint WM_MOVE = 0x0003;
    private const uint WM_HOTKEY = 0x0312;

    //
    // Creators
    //

    public static void Open(string[] args)
    {
        CoroutineUtils.Start(async () =>
        {
            Route? route = await GetFileActivatedComicRoute(args);
            if (route is null)
            {
                Open(recoverTabs: true);
                return;
            }

            Open(route.Url, recoverTabs: false);
        });
    }

    public static void Open(string url = "", bool recoverTabs = false)
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN)
            .WithParam(RouterConstants.ARG_RECOVER_TABS, recoverTabs ? "1" : "0");

        if (!string.IsNullOrEmpty(url))
        {
            route.WithParam(RouterConstants.ARG_URL, url);
        }

        MainWindow window = new(route.Url);
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

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }
    public bool Alive { get; private set; } = false;

    //
    // Constructors
    //

    private MainWindow(string url)
    {
        _members = new();
        Members._url = url;

        InitializeComponent();

        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        if (DebugUtils.DeveloperMode)
        {
            RegisterMessageLoop();
            HotKeyManager.Instance.RegisterHotKeys(WindowHandle);
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

    public bool IsActive => PInvoke.GetActiveWindow() == new Windows.Win32.Foundation.HWND(WindowHandle.ToInt32());

    public void OnCommandLine(string[] args)
    {
        _ = OnCommandLineAsync(args);
    }

    //
    // Event Handlers
    //

    // IMPORTANT: Handle event registration and unregistration in the code-behind file,
    // do not use XAML for this purpose in order to prevent memory leaks.

    private void SubscribeEvents()
    {
        PageFrame.Loaded += OnPageFrameLoaded;
        Closed += OnWindowClosed;
        SizeChanged += OnWindowSizeChanged;
    }

    private void UnsubscribeEvents()
    {
        PageFrame.Loaded -= OnPageFrameLoaded;
        Closed -= OnWindowClosed;
        SizeChanged -= OnWindowSizeChanged;
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        ScheduleSavingWindowPlacement();
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        // Load the main page
        Route route = Route.Create(Members._url)
            .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString());
        NavigationBundle bundle = AppRouter.Process(route)!;
        bundle.Communicator.RegisterAbility<ICommonPageAbility>(Members._mainWindowAbility);
        PageFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        Members._mainPage = (MainPage)PageFrame.Content;

        // Mark the beginning of the window lifecycle
        Alive = true;

        // Restore window placement
        TryRestoreWindowPlacement();

        // Show last crash report if applicable
        if (WindowMembers.sCanReportCrash)
        {
            WindowMembers.sCanReportCrash = false;
            if (!App.ExitedNormallyLastTime && DebugUtils.DebugMode)
            {
                DebugUtils.ReportLastCrash();
            }
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        // Mark the end of the window lifecycle
        Alive = false;

        // Close all tabs and dispatch page stopped event
        Members._mainPage!.CloseAllTabs();
        Members._mainWindowAbility.DispatchPageStoppedEvent();

        // Unsubscribe window events
        UnsubscribeEvents();

        // Unregister hotkeys and message loop
        HotKeyManager.Instance.UnregisterHotKeys(WindowHandle);
        UnregisterMessageLoop();

        // Unregister window from WindowManager
        App.WindowManager.UnregisterWindow(WindowId);

        // Use another window to register hotkeys again
        MainWindow? anyWindow = App.WindowManager.GetAnyWindow();
        if (anyWindow != null)
        {
            HotKeyManager.Instance.RegisterHotKeys(anyWindow.WindowHandle);
        }

        // Dereference all members
        _members = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    //
    // Win32 Message Loop
    //

    private void RegisterMessageLoop()
    {
        Windows.Win32.Foundation.HWND hwnd = new(WindowHandle.ToInt32());
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
            Logger.AssertNotReachHere("Failed to set window procedure.");
            return;
        }

        Members._originProc = Marshal.GetDelegateForFunctionPointer<Windows.Win32.UI.WindowsAndMessaging.WNDPROC>(prevWndProc);
    }

    private void UnregisterMessageLoop()
    {
        if (Members._originProc != null && Members._wndProcDelegate != null && WindowHandle != IntPtr.Zero)
        {
            Windows.Win32.Foundation.HWND hwnd = new(WindowHandle.ToInt32());
            nint originProcPtr = Marshal.GetFunctionPointerForDelegate(Members._originProc);
#if x86
            PInvoke.SetWindowLong(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, (int)originProcPtr);
#elif x64 || ARM64
            PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, originProcPtr);
#endif

            Members._originProc = null;
            Members._wndProcDelegate = null;
        }
    }

    private Windows.Win32.Foundation.LRESULT MessageLoopProc(Windows.Win32.Foundation.HWND hwnd,
        uint uMsg,
        Windows.Win32.Foundation.WPARAM wParam,
        Windows.Win32.Foundation.LPARAM lParam)
    {
        switch (uMsg)
        {
            case WM_MOVE:
                ScheduleSavingWindowPlacement();
                break;
            case WM_HOTKEY:
                {
                    int hotkeyId = (int)wParam.Value;
                    if (HotKeyManager.Instance.HandleHotKey(hotkeyId))
                    {
                        return (Windows.Win32.Foundation.LRESULT)IntPtr.Zero;
                    }
                }
                break;
            default:
                break;
        }

        return PInvoke.CallWindowProc(Members._originProc, hwnd, uMsg, wParam, lParam);
    }

    //
    // File Activation
    //

    private async Task OnCommandLineAsync(string[] args)
    {
        Route? route = await GetFileActivatedComicRoute(args);
        if (route is null)
        {
            return;
        }

        if (Members._mainPage is null)
        {
            Members._url = route.Url;
            return;
        }

        Members._mainPage.OpenInNewTab(route);

        if (WindowHandle != IntPtr.Zero)
        {
            PInvoke.SetForegroundWindow(new Windows.Win32.Foundation.HWND(WindowHandle.ToInt32()));
        }
    }

    private static async Task<Route?> GetFileActivatedComicRoute(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string targetFilePath = args[0];
        if (!File.Exists(targetFilePath))
        {
            Logger.W("GetFileActivatedComicRoute", "Target file does not exist: " + targetFilePath);
            return null;
        }

        string targetFileExtension = Path.GetExtension(targetFilePath);
        if (!AppInfoProvider.IsSupportedExternalFileExtension(targetFileExtension))
        {
            return null;
        }

        StorageFile? targetFile = await Storage.TryGetFile(targetFilePath);
        if (targetFile is null)
        {
            Logger.W("GetFileActivatedComicRoute", "Failed to get target file: " + targetFilePath);
            return null;
        }

        ComicModel? comic = await ComicModel.FromFile(targetFile);
        if (comic is not null)
        {
            if (comic.IsExternal)
            {
                return Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                    .WithParam(RouterConstants.ARG_COMIC_LOCATION, targetFile.Path);
            }
            else
            {
                return Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                    .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            }
        }

        if (AppInfoProvider.IsSupportedImageExtension(targetFile.FileType))
        {
            string parentPath = targetFile.Path;
            parentPath = StringUtils.ParentLocationFromLocation(parentPath);
            comic = await ComicModel.FromLocation(parentPath, "GetFileActivatedComicRoute");
            if (comic is not null && !comic.IsExternal)
            {
                return Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                    .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            }

            return Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_LOCATION, parentPath);
        }

        return null;
    }

    //
    // Window Placement
    //

    private void ScheduleSavingWindowPlacement()
    {
        if (!Alive || Members._saveWindowPlacementScheduled)
        {
            return;
        }

        Members._saveWindowPlacementScheduled = true;
        Task.Delay(500).ContinueWith(delegate
        {
            if (!Alive)
            {
                return;
            }

            Members._saveWindowPlacementScheduled = false;
            NativeMethods.GetWindowPlacement(WindowHandle, out NativeModels.WindowPlacement placement);
            string serialized = JsonSerializer.Serialize(placement);
            KVDatabase.Default.With(DatabaseEntry.KV_LIB_APP).SetString(DatabaseEntry.KV_KEY_APP_WINDOW_STATES, serialized);
        });
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

    private void TryRestoreWindowPlacement()
    {
        if (!Alive)
        {
            return;
        }

        string? windowStates = KVDatabase.Default.GetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_WINDOW_STATES);
        if (string.IsNullOrEmpty(windowStates))
        {
            return;
        }

        NativeModels.WindowPlacement windowPlacement;
        try
        {
            windowPlacement = JsonSerializer.Deserialize<NativeModels.WindowPlacement>(windowStates);
        }
        catch (Exception)
        {
            return;
        }

        NativeMethods.SetWindowPlacement(WindowHandle, ref windowPlacement);
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

    private class MainWindowAbility : ICommonPageAbility
    {
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
    }

    //
    // Types
    //

    private class WindowMembers
    {
        public static bool sCanReportCrash = true;

        public MainPage? _mainPage;
        public string _url = string.Empty;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _originProc;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _wndProcDelegate;
        public readonly MainWindowAbility _mainWindowAbility = new();
        public bool _saveWindowPlacementScheduled = false;
    }
}
