// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
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

using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Win32;

using WinRT.Interop;

namespace ComicReader;

public sealed partial class MainWindow : Window
{
    private const uint WM_HOTKEY = 0x0312;

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

    //
    // Constructors
    //

    public MainWindow(string url)
    {
        WindowMembers members = new();
        _members = members;
        Members._url = url;

        InitializeComponent();

        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        if (DebugUtils.DeveloperMode)
        {
            RegisterMessageLoop();

            Members._hotKeyManager = new(WindowId);
            Members._hotKeyManager.RegisterHotKeys(WindowHandle);
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

    public void OnFileActivated(FileActivatedEventArgs args)
    {
        _ = OnFileActivatedAsync(args);
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
        if (Members._mainPage == null)
        {
            return;
        }

        var placement = new NativeModels.WindowPlacement();
        NativeMethods.GetWindowPlacement(WindowHandle, out placement);
        string serialized = JsonSerializer.Serialize(placement);
        KVDatabase.Default.With(DatabaseEntry.KV_LIB_APP).SetString(DatabaseEntry.KV_KEY_APP_WINDOW_STATES, serialized);
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        TryRecoverWindowStates();

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN)
            .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString())
            .WithParam(RouterConstants.ARG_URL, Members._url);
        NavigationBundle bundle = AppRouter.Process(route)!;
        bundle.Communicator.RegisterAbility<ICommonPageAbility>(Members._mainWindowAbility);
        PageFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        Members._mainPage = (MainPage)PageFrame.Content;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Members._mainPage!.CloseAllTabs();
        Members._mainWindowAbility.DispatchPageStoppedEvent();
        UnsubscribeEvents();
        UnregisterMessageLoop();
        App.WindowManager.UnregisterWindow(WindowId);

        _members = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    //
    // Win32
    //

    private void RegisterMessageLoop()
    {
        Windows.Win32.Foundation.HWND hwnd = new(WindowHandle.ToInt32());
        var wndProcDelegate = new Windows.Win32.UI.WindowsAndMessaging.WNDPROC(MessageLoopProc);
        Members._wndProcDelegate = wndProcDelegate;
        nint wndPrcPointer = Marshal.GetFunctionPointerForDelegate(wndProcDelegate);
        nint prevWndProc = PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, wndPrcPointer);
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
            PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, originProcPtr);

            Members._originProc = null;
            Members._wndProcDelegate = null;
        }
    }

    private Windows.Win32.Foundation.LRESULT MessageLoopProc(Windows.Win32.Foundation.HWND hwnd,
        uint uMsg,
        Windows.Win32.Foundation.WPARAM wParam,
        Windows.Win32.Foundation.LPARAM lParam)
    {
        if (uMsg == WM_HOTKEY)
        {
            int hotkeyId = (int)wParam.Value;
            if (Members._hotKeyManager != null && Members._hotKeyManager.HandleHotKey(hotkeyId))
            {
                return (Windows.Win32.Foundation.LRESULT)IntPtr.Zero;
            }
        }

        return PInvoke.CallWindowProc(Members._originProc, hwnd, uMsg, wParam, lParam);
    }

    //
    // File Activation
    //

    private async Task OnFileActivatedAsync(FileActivatedEventArgs args)
    {
        ComicModel? comic = await GetStartupComic(args);
        if (comic == null)
        {
            return;
        }

        string token = AppModel.PutComicData(comic);
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_TOKEN, token);

        if (Members._mainPage == null)
        {
            Members._url = route.Url;
            return;
        }

        Members._mainPage.OpenInNewTab(route);
    }

    private async Task<ComicModel?> GetStartupComic(FileActivatedEventArgs args)
    {
        var target_file = (StorageFile)args.Files[0];

        if (!AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
        {
            return null;
        }

        ComicModel? comic = await ComicModel.FromFile(target_file);

        if (comic == null && AppInfoProvider.IsSupportedImageExtension(target_file.FileType))
        {
            string dir = target_file.Path;
            dir = StringUtils.ParentLocationFromLocation(dir);
            comic = await ComicModel.FromLocation(dir, "MainGetStartupComicFromImage");

            if (comic == null)
            {
                var all_files = new List<StorageFile>();
                var img_files = new List<StorageFile>();
                StorageFileQueryResult neighboring_file_query =
                    args.NeighboringFilesQuery;

                if (neighboring_file_query != null)
                {
                    IReadOnlyList<StorageFile> files = await args.NeighboringFilesQuery.GetFilesAsync();
                    all_files = [.. files];
                }

                if (all_files.Count == 0)
                {
                    foreach (IStorageItem item in args.Files)
                    {
                        if (item is StorageFile file)
                        {
                            all_files.Add(file);
                        }
                    }
                }

                foreach (StorageFile file in all_files)
                {
                    if (AppInfoProvider.IsSupportedImageExtension(file.FileType))
                    {
                        img_files.Add(file);
                    }
                }

                comic = ComicModel.FromImageFiles(dir, img_files);
            }
        }

        return comic;
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

    private void TryRecoverWindowStates()
    {
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
        public MainPage? _mainPage;
        public string _url = string.Empty;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _originProc;
        public Windows.Win32.UI.WindowsAndMessaging.WNDPROC? _wndProcDelegate;
        public HotKeyManager? _hotKeyManager;
        public readonly MainWindowAbility _mainWindowAbility = new();
    }
}
