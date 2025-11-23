// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.InitTask;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Native;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.AppWindows.Main;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace ComicReader;

public partial class App : Application
{
    private const string TAG = nameof(App);
    private const string COMMAND_LINE_FILE_NAME = "command_line.txt";

    private static App? _instance;
    public static App Instance => _instance!;

    private readonly InitTaskManager _initTaskManager;

    internal readonly WindowManager WindowManager = new();
    internal bool ExitedNormallyLastTime { get; private set; } = true;

    public App()
    {
        _instance = this;
        _initTaskManager = new(this);
        _initTaskManager.InitOnAppCreate();
        ExitedNormallyLastTime = _initTaskManager.ExitedNormallyLastTime;
        InitializeComponent();
    }

    //
    // Public Methods
    //

    internal void OnCommandLine(MainWindow window, string[] args)
    {
        CoroutineUtils.Start(async () =>
        {
            Route? route = await GetFileActivatedComicRoute(args);
            if (!window.Alive)
            {
                Logger.E(TAG, "Unable to process command line because window is not alive.");
                return;
            }

            if (route is not null)
            {
                window.OpenTab(route.Url, true);
            }

            window.BringToFront();
        });
    }

    //
    // Lifecycle
    //

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
    {
        // Read: https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle#single-instancing-in-applicationonlaunched
        // If this is the first instance launched, then register it as the "main" instance.
        // If this isn't the first instance launched, then "main" will already be registered,
        // so retrieve it.
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        AppActivationArguments activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // If the instance that's executing the OnLaunched handler right now
        // isn't the "main" instance.
        if (!mainInstance.IsCurrent)
        {
            // If the app is running in portable mode, store the command line arguments
            if (EnvironmentProvider.IsPortable())
            {
                StoreCommandLine();
            }

            // Redirect the activation (and args) to the "main" instance, and exit.
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        _initTaskManager.InitOnAppLaunch();
        mainInstance.Activated += OnActivated;
        OnActivatedInternal(activatedEventArgs, firstLaunch: true);
    }

    private void OnActivated(object? sender, AppActivationArguments e)
    {
        OnActivatedInternal(e, firstLaunch: false);
    }

    private void OnActivatedInternal(AppActivationArguments e, bool firstLaunch)
    {
        string[] cmdArgs;
        if (EnvironmentProvider.IsPortable())
        {
            string? commandLine;
            if (firstLaunch)
            {
                commandLine = Environment.CommandLine;
            }
            else
            {
                commandLine = TryReadCommandLine();
                if (string.IsNullOrEmpty(commandLine))
                {
                    return;
                }
            }

            string[] splitedCommandLine = SplitCommandLine(commandLine);
            if (splitedCommandLine.Length >= 1)
            {
                cmdArgs = splitedCommandLine[1..];
            }
            else
            {
                cmdArgs = splitedCommandLine;
            }
        }
        else
        {
            switch (e.Kind)
            {
                case ExtendedActivationKind.File:
                    {
                        var fileArgs = (FileActivatedEventArgs)e.Data;
                        cmdArgs = [fileArgs.Files[0].Path];
                    }
                    break;
                default:
                    cmdArgs = [];
                    break;
            }
        }

        string cmd = string.Join(' ', cmdArgs);
        Logger.I(TAG, $"OnActivated: firstLaunch={firstLaunch}, cmd={cmd}");

        if (firstLaunch)
        {
            CoroutineUtils.Start(async () =>
            {
                Route? route = await GetFileActivatedComicRoute(cmdArgs);
                if (route is null)
                {
                    WindowManager.RestoreWindowStatus();
                    return;
                }

                MainWindow.Open(route.Url, restorePlacement: true);
            });
        }
        else
        {
            MainWindow? window = WindowManager.GetAnyWindow();
            if (window is null)
            {
                Logger.F(TAG, "Failed to perform file activation, no window is found.");
                return;
            }

            OnCommandLine(window, cmdArgs);
        }
    }

    private static void StoreCommandLine()
    {
        string commandLine = Environment.CommandLine;
        string temporaryFolderPath = StorageLocation.TemporaryFolderPath;
        if (!Directory.Exists(temporaryFolderPath))
        {
            Directory.CreateDirectory(temporaryFolderPath);
        }

        string commandLineFile = Path.Combine(temporaryFolderPath, COMMAND_LINE_FILE_NAME);
        File.WriteAllText(commandLineFile, commandLine);
    }

    private static string? TryReadCommandLine()
    {
        string temporaryFolderPath = StorageLocation.TemporaryFolderPath;
        if (!Directory.Exists(temporaryFolderPath))
        {
            return null;
        }

        string commandLineFile = Path.Combine(temporaryFolderPath, COMMAND_LINE_FILE_NAME);
        if (!File.Exists(commandLineFile))
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(commandLineFile);
        }
        catch (Exception e)
        {
            Logger.F(TAG, nameof(TryReadCommandLine), e);
            return null;
        }

        try
        {
            File.Delete(commandLineFile);
        }
        catch (Exception e)
        {
            Logger.F(TAG, nameof(TryReadCommandLine), e);
        }

        return content;
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine))
        {
            return [];
        }

        IntPtr argv = NativeMethods.CommandLineToArgvW(commandLine, out int argc);
        if (argv == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; i++)
            {
                IntPtr p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                args[i] = Marshal.PtrToStringUni(p)!;
            }

            return args;
        }
        finally
        {
            NativeMethods.LocalFree(argv);
        }
    }

    //
    // File Activation
    //

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
}
