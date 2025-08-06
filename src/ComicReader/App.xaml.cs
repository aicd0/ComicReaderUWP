// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common;
using ComicReader.Common.InitTask;
using ComicReader.SDK.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;

namespace ComicReader;

public partial class App : Application
{
    private const string TAG = nameof(App);

    internal static readonly WindowManager<MainWindow> WindowManager = new();

    private readonly InitTaskManager _initTaskManager;

    public App()
    {
        _initTaskManager = new(this);
        _initTaskManager.InitOnAppCreate();
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
            // Redirect the activation (and args) to the "main" instance, and exit.
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        _initTaskManager.InitOnAppLaunch();

        // Initialize MainWindow here
        MainWindow.Open(recoverTabs: true);

        mainInstance.Activated += OnActivated;
        OnActivated(null, activatedEventArgs);
    }

    private void OnActivated(object? sender, AppActivationArguments e)
    {
        if (e.Kind == ExtendedActivationKind.File)
        {
            MainWindow? window = WindowManager.GetAnyWindow();
            if (window != null)
            {
                window.OnFileActivated((FileActivatedEventArgs)e.Data);
            }
            else
            {
                Logger.F(TAG, "Failed to perform file activation, no window is found.");
            }
        }
    }
}
