// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Constants;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.Native;
using ComicReader.Views.Main;

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;

using WinRT.Interop;

namespace ComicReader;

public sealed partial class MainWindow : Window
{
    //
    // Member variables
    //

    // IMPORTANT: Any object referenced by this class need be dereferenced in OnWindowClosed,
    // or else memory leaks will occur.
    // See http://github.com/microsoft/microsoft-ui-xaml/issues/7282 for more details.

    private MainPage _mainPage;
    private string _url;

    //
    // Constructors
    //

    public MainWindow(string url)
    {
        _url = url;

        InitializeComponent();

        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        Title = StringResourceProvider.Instance.AppDisplayName;
        ExtendsContentIntoTitleBar = true;
        TrySetAcrylicBackdrop();
        SetWindowIcon();
        SubscribeEvents();
    }

    //
    // Public Interfaces
    //

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }

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
        if (_mainPage == null)
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
            .WithParam(RouterConstants.ARG_URL, _url);
        AppRouter.OpenInFrame(PageFrame, route);
        _mainPage = (MainPage)PageFrame.Content;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _mainPage.CloseAllTabs();

        UnsubscribeEvents();
        App.WindowManager.UnregisterWindow(WindowId);
        _mainPage = null;
        _url = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    //
    // File Activation
    //

    private async Task OnFileActivatedAsync(FileActivatedEventArgs args)
    {
        ComicModel comic = await GetStartupComic(args);
        if (comic == null)
        {
            return;
        }

        string token = AppModel.PutComicData(comic);
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_TOKEN, token);

        if (_mainPage == null)
        {
            _url = route.Url;
            return;
        }

        _mainPage.OpenInNewTab(route);
    }

    private async Task<ComicModel> GetStartupComic(FileActivatedEventArgs args)
    {
        var target_file = (StorageFile)args.Files[0];

        if (!AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
        {
            return null;
        }

        ComicModel comic = await ComicModel.FromFile(target_file);

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
        string windowStates = KVDatabase.Default.GetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_WINDOW_STATES);
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
        nint hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(@"Assets\AppIcon.ico");
    }
}
