// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReaderUWP.Common.InitTask;

internal class InitTaskManager(Application application)
{
    private const string TAG = nameof(InitTaskManager);

    private readonly Application _application = application;
    private object? _appLock;

    public bool ExitedNormallyLastTime { get; private set; } = true;
    public bool IsFirstInstance { get; private set; } = true;
    public bool SafeMode { get; private set; } = false;

    public void InitOnAppCreate()
    {
        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());
        ServiceManager.RegisterService<INativeService>(new NativeService());

        // Register crash handler
        _application.UnhandledException += (_, e) =>
        {
            DebugUtils.CaptureFatalError("An unknown error occurred in the application.", e.Exception);
        };
        SynchronizationContext.SetSynchronizationContext(
            new AppSynchronizationContext(SynchronizationContext.Current!));

        LaunchPerformanceTracker.MarkAppEntry();

        MainThreadUtils.Initialize(DispatcherQueue.GetForCurrentThread());

        IsFirstInstance = TryRegisterFirstInstance();
        if (!IsFirstInstance)
        {
            return;
        }

        EnvironmentProvider.Instance.Initialize(SecretImpl.AdditionalDebugInformation);
        SentryManager.Initialize(SecretImpl.SentryDsn, EnvironmentProvider.Instance.GetEnvironmentTags());
        AppDB.Initialize();
        InitializeAppLanguage();

        if (!ExitedNormallyLastTime)
        {
            NativeDialog.DialogResult result = NativeDialog.ShowYesNoCancel(
                StringResourceProvider.Instance.AppDisplayName,
                StringResourceProvider.Instance.SafeModeMessage);
            switch (result)
            {
                case NativeDialog.DialogResult.Yes:
                    SafeMode = true;
                    break;
                case NativeDialog.DialogResult.No:
                    SafeMode = false;
                    break;
                case NativeDialog.DialogResult.Cancel:
                default:
                    AppExitHandler();
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                    return;
            }
        }

        Logger.I(TAG, $"App launched (SafeMode={SafeMode})");
        RegisterExitHandler();
        InitializeAppTheme();
    }

    public void InitOnAppLaunch()
    {
        DebugUtils.Initialize();
        ImageCacheManager.Initialize(Path.Combine(StorageLocation.LocalCacheFolderPath, "image_cache"), clear: false);
        FocusTracker.Initialize();
        PluginManager.Instance.LoadPlugins();
    }

    private bool TryRegisterFirstInstance()
    {
        string lockFileDirPath = StorageLocation.TemporaryFolderPath;
        Directory.CreateDirectory(lockFileDirPath);
        string lockFilePath = Path.Combine(lockFileDirPath, "app.lock");
        if (File.Exists(lockFilePath))
        {
            ExitedNormallyLastTime = false;
        }

        try
        {
            var fileStream = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            fileStream.Lock(0, 0);
            _appLock = fileStream;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void RegisterExitHandler()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            AppExitHandler();
        };
    }

    private void AppExitHandler()
    {
        Logger.Flush();
        AppDB.Dispose();

        if (_appLock is FileStream fileStream)
        {
            try
            {
                fileStream.Unlock(0, 0);
                fileStream.Dispose();
                File.Delete(fileStream.Name);
                _appLock = null;
            }
            catch (Exception)
            {
            }
        }
    }

    private static void InitializeAppTheme()
    {
        AppSettingsModel.AppearanceSetting themeSetting = AppSettingsModel.Instance.GetModel().Theme;
        switch (themeSetting)
        {
            case AppSettingsModel.AppearanceSetting.Light:
                Application.Current.RequestedTheme = ApplicationTheme.Light;
                break;
            case AppSettingsModel.AppearanceSetting.Dark:
                Application.Current.RequestedTheme = ApplicationTheme.Dark;
                break;
            default:
                break;
        }
    }

    private static void InitializeAppLanguage()
    {
        if (EnvironmentProvider.IsPortable())
        {
            string languageTag = AppSettingsModel.Instance.Language;
            if (string.IsNullOrEmpty(languageTag))
            {
                languageTag = EnvironmentProvider.GetCurrentSystemLanguage();
            }

            ApplicationLanguages.PrimaryLanguageOverride = languageTag;
            EnvironmentProvider.Instance.SetCurrentAppLanguage(languageTag);
        }
    }
}
