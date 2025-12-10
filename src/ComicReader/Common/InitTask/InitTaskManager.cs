// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

using ComicReader.Common.Imaging;
using ComicReader.Common.Services;
using ComicReader.Data;
using ComicReader.Data.Models;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.ServiceManagement;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReader.Common.InitTask;

internal class InitTaskManager(Application application)
{
    private readonly Application _application = application;

    private object? _appLock;

    public bool ExitedNormallyLastTime { get; private set; } = true;

    public void InitOnAppCreate()
    {
        DebugUtils.TrackError(InitOnAppCreateInternal, fastFail: true);
    }

    public void InitOnAppLaunch()
    {
        DebugUtils.TrackError(InitOnAppLaunchInternal, fastFail: true);
    }

    private void InitOnAppCreateInternal()
    {
        // Register crash handler
        _application.UnhandledException += (_, e) =>
        {
            DebugUtils.CaptureFatalError(e.Message, e.Exception);
        };
        SynchronizationContext.SetSynchronizationContext(
            new AppSynchronizationContext(SynchronizationContext.Current!));

        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());

        // Initialize main thread dispatcher
        MainThreadUtils.Initialize(DispatcherQueue.GetForCurrentThread());

        // Initialize environment information
        EnvironmentProvider.Instance.Initialize(Properties.AdditionalDebugInformation);

        bool isFirstInstance = TryRegisterFirstInstance();
        if (isFirstInstance)
        {
            // Register exit handler
            RegisterExitHandler();

            // Initialize Sentry
            SentryManager.Initialize(Properties.SentryDsn, EnvironmentProvider.GetEnvironmentTags());

            // Initialize app language
            InitializeAppLanguage();

            // Initialize app theme
            InitializeAppTheme();
        }
    }

    private void InitOnAppLaunchInternal()
    {
        // Initialize debug tools
        DebugUtils.Initialize();

        // Initialize imaging service
        ImageCacheManager.Initialize(Path.Combine(StorageLocation.LocalCacheFolderPath, "image_cache"), clear: false);

        // Initialize database
        DatabaseUpgradeManager.Instance.UpgradeDatabaseBeforeInitialization();
        SqlDatabaseManager.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabaseAfterInitialization();

        // Initialize focus tracker
        FocusTracker.Initialize();
    }

    private void InitializeAppTheme()
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

    private void InitializeAppLanguage()
    {
        if (EnvironmentProvider.IsPortable())
        {
            string languageTag = AppSettingsModel.Instance.GetModel().Language;
            if (string.IsNullOrEmpty(languageTag))
            {
                languageTag = EnvironmentProvider.GetCurrentSystemLanguage();
            }
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;
            EnvironmentProvider.Instance.SetCurrentAppLanguage(languageTag);
        }
    }

    private bool TryRegisterFirstInstance()
    {
        string lockFileDirPath = StorageLocation.TemporaryFolderPath;
        if (!Directory.Exists(lockFileDirPath))
        {
            Directory.CreateDirectory(lockFileDirPath);
        }

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
            Logger.Flush();
            KVDatabase.Dispose();
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
        };
    }
}
