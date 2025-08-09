// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using ComicReader.Common.Imaging;
using ComicReader.Common.Services;
using ComicReader.Data;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.ServiceManagement;
using ComicReader.SDK.Common.Storage;

using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReader.Common.InitTask;

internal class InitTaskManager(Application application)
{
    private readonly Application _application = application;

    private object? _appLock;

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

        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());

        // Initialize environment information
        EnvironmentProvider.Instance.Initialize(Properties.AdditionalDebugInformation);

        bool isFirstInstance = TryRegisterFirstInstance();
        if (isFirstInstance)
        {
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

        // Initialize logger
        Logger.Initialize();

        // Initialize imaging service
        ImageCacheManager.Initialize(Path.Combine(StorageLocation.LocalCacheFolderPath, "image_cache"), clear: false);

        // Initialize database
        DatabaseUpgradeManager.Instance.UpgradeDatabaseBeforeInitialization();
        XmlDatabaseManager.Initialize();
        SqlDatabaseManager.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabaseAfterInitialization();

        // Update comic library
        ComicModel.UpdateAllComics("InitOnAppLaunchInternal");
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
}
