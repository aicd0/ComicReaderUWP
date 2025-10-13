// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;

using ComicReader.Common;
using ComicReader.Common.InitTask;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Native;
using ComicReader.SDK.Common.Storage;
using ComicReader.Views.AppWindows.Main;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;

namespace ComicReader;

public partial class App : Application
{
    private const string TAG = nameof(App);
    private const string COMMAND_LINE_FILE_NAME = "command_line.txt";

    private readonly InitTaskManager _initTaskManager;

    internal static readonly WindowManager WindowManager = new();
    internal static bool ExitedNormallyLastTime { get; private set; } = true;

    public App()
    {
        _initTaskManager = new(this);
        _initTaskManager.InitOnAppCreate();
        ExitedNormallyLastTime = _initTaskManager.ExitedNormallyLastTime;
        InitializeComponent();
    }

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
            MainWindow.Open(cmdArgs);
        }
        else
        {
            MainWindow? window = WindowManager.GetAnyWindow();
            if (window is null)
            {
                Logger.F(TAG, "Failed to perform file activation, no window is found.");
                return;
            }

            window.OnCommandLine(cmdArgs);
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
}
