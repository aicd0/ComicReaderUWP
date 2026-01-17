// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.AppEnvironment;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.ServiceManagement;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReaderUWP.Common.InitTask;

internal class InitTaskManager(Application application)
{
    private readonly Application _application = application;

    private object? _appLock;

    public bool IsFirstInstance { get; private set; } = true;
    public bool IsExitedNormallyLastTime { get; private set; } = true;

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

        IsFirstInstance = TryRegisterFirstInstance();
        if (IsFirstInstance)
        {
            // Register exit handler
            RegisterExitHandler();

            // Initialize environment information
            EnvironmentProvider.Instance.Initialize(SecretImpl.AdditionalDebugInformation);

            // Initialize Sentry
            SentryManager.Initialize(SecretImpl.SentryDsn, EnvironmentProvider.Instance.GetEnvironmentTags());

            // Initialize databases
            AppDB.Initialize();

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

        // Initialize focus tracker
        FocusTracker.Initialize();

        // Load plugins
        PluginManager.Instance.LoadPlugins();
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
            string languageTag = AppSettingsModel.Instance.Language;
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
        Directory.CreateDirectory(lockFileDirPath);
        string lockFilePath = Path.Combine(lockFileDirPath, "app.lock");
        if (File.Exists(lockFilePath))
        {
            IsExitedNormallyLastTime = false;
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
        };
    }
}
