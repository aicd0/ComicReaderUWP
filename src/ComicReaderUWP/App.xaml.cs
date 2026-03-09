// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.InitTask;
using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.AppEnvironment;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.Views.AppWindows.Main;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ComicReaderUWP;

public partial class App : Application
{
    private const string TAG = nameof(App);
    private const string COMMAND_LINE_FILE_NAME = "CommandLine.txt";

    private static App? _instance;
    public static App Instance => _instance!;

    private readonly InitTaskManager _initTaskManager;

    internal readonly WindowManager WindowManager = new();
    internal bool ExitedNormallyLastTime => _initTaskManager.IsExitedNormallyLastTime;

    public App()
    {
        LaunchPerformanceTracker.MarkAppEntry();
        _instance = this;
        _initTaskManager = new(this);
        _initTaskManager.InitOnAppCreate();
        InitializeComponent();
    }

    //
    // Public Methods
    //

    internal async Task OnCommandLine(MainWindow window, string[] args)
    {
        Route? route = await GetFileActivatedRoute(args);
        if (route is not null)
        {
            window.OpenTab(route.Url, string.Empty, string.Empty);
        }

        window.BringToFront();
    }

    //
    // Lifecycle
    //

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
    {
        LaunchPerformanceTracker.MarkAppLaunched();
        AppActivationArguments activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        bool isFirstInstance = _initTaskManager.IsFirstInstance;
        bool isMainInstance = mainInstance.IsCurrent;

        if (isMainInstance != isFirstInstance)
        {
            Logger.F(TAG, $"Inconsistent startup state: FirstInstance={isFirstInstance}, MainInstance={isMainInstance}");
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        if (!isMainInstance)
        {
            if (EnvironmentProvider.IsPortable())
            {
                StoreCommandLine();
            }

            CoroutineUtils.Run(async () =>
            {
                await mainInstance.RedirectActivationToAsync(activatedEventArgs);
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            });

            return;
        }

        _initTaskManager.InitOnAppLaunch();
        mainInstance.Activated += OnActivated;

        CoroutineUtils.Run(async () =>
        {
            await OnActivatedInternal(activatedEventArgs, firstLaunch: true);
        });
    }

    private void OnActivated(object? sender, AppActivationArguments e)
    {
        CoroutineUtils.RunInMainThreadAsync(async () =>
        {
            await OnActivatedInternal(e, firstLaunch: false);
        });
    }

    private async Task OnActivatedInternal(AppActivationArguments e, bool firstLaunch)
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
            WindowManager.RestoreWindowStatus();
        }

        MainWindow? window = WindowManager.GetAnyWindow();
        if (window is null)
        {
            Route? route = await GetFileActivatedRoute(cmdArgs);
            if (route is not null)
            {
                MainWindow.Open(route.Url, restorePlacement: true);
            }
            else
            {
                MainWindow.Open();
            }

            return;
        }

        await OnCommandLine(window, cmdArgs);
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

        unsafe
        {
            PWSTR* argv = PInvoke.CommandLineToArgv(commandLine, out int argc);
            if (argv == null)
            {
                return [];
            }

            try
            {
                string[] args = new string[argc];
                for (int i = 0; i < argc; i++)
                {
                    args[i] = argv[i].ToString();
                }

                return args;
            }
            finally
            {
                PInvoke.LocalFree((HLOCAL)argv);
            }
        }
    }

    //
    // File Activation
    //

    private static async Task<Route?> GetFileActivatedRoute(string[] args)
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
            return OpenComicHelper.GetComicRoute(comic, null);
        }

        if (AppInfoProvider.IsSupportedImageExtension(targetFile.FileType))
        {
            string parentPath = targetFile.Path;
            parentPath = StringUtils.ParentLocationFromLocation(parentPath);
            comic = await ComicModel.FromLocation(parentPath, "GetFileActivatedComicRoute") ??
                await ComicModel.FromExternalLocation(parentPath);
            if (comic is not null)
            {
                return OpenComicHelper.GetComicRoute(comic, null);
            }
        }

        return null;
    }
}
