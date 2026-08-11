// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Models;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReaderUWP.Common.InitTask;

internal class InitTaskManager
{
    private const string TAG = nameof(InitTaskManager);

    public static InitTaskManager Instance { get; } = new();

    private object? _appLock;

    public bool ExitedNormallyLastTime { get; private set; } = true;
    public bool IsFirstInstance { get; private set; } = true;
    public bool SafeMode { get; private set; } = false;

    private InitTaskManager() { }

    public void InitOnMain()
    {
        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());
        ServiceManager.RegisterService<INativeService>(new NativeService());

        // Register crash handler
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception ex = e.ExceptionObject is Exception exception ? exception : new Exception(e.ExceptionObject?.ToString());
            DebugUtils.CaptureFatalError("An unknown error occurred in a managed thread.", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            DebugUtils.CaptureFatalError("An unknown error occurred in a background task.", e.Exception);
        };

        DatabaseUpgradeManager.UpgradeDatabaseBeforeInitialization();

        IsFirstInstance = TryRegisterFirstInstance();
        RegisterExitHandler();

        EnvironmentProvider.Instance.Initialize(Secret.ExtraDebugFields);
        SentryManager.Initialize(Secret.SentryDsn, EnvironmentProvider.Instance.GetEnvironmentTags());

        if (DebugModel.WaitForDebugger)
        {
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                NativeDialogResult result = ServiceManager.GetService<INativeService>().ShowDialog(
                    NativeDialogButtonType.OKCancel,
                    NativeDialogIconType.Info,
                    "Comic Reader UWP",
                    "The app is launched in WaitForDebugger mode. Attach a debugger then click OK.");
                if (result != NativeDialogResult.OK)
                {
                    break;
                }
            }
        }
    }

    public void InitOnAppCreate(Application application)
    {
        // Register crash handler
        application.UnhandledException += (_, e) =>
        {
            DebugUtils.CaptureFatalError("An unknown error occurred in the UI thread.", e.Exception);
        };
        SynchronizationContext.SetSynchronizationContext(
            new AppSynchronizationContext(SynchronizationContext.Current!));

        MainThreadUtils.Initialize(DispatcherQueue.GetForCurrentThread());

        if (!IsFirstInstance)
        {
            return;
        }

        AppDB.Initialize();
        DatabaseUpgradeManager.UpgradeDatabaseAfterInitialization();

        InitializeAppLanguage();

        if (!ExitedNormallyLastTime)
        {
            INativeService nativeService = ServiceManager.GetService<INativeService>();
            NativeDialogResult dialogResult = nativeService.ShowDialog(
                NativeDialogButtonType.YesNoCancel,
                NativeDialogIconType.Info,
                StringResourceProvider.Instance.AppDisplayName,
                StringResourceProvider.Instance.SafeModeMessage);
            switch (dialogResult)
            {
                case NativeDialogResult.Yes:
                    SafeMode = true;
                    break;
                case NativeDialogResult.No:
                    SafeMode = false;
                    break;
                default:
                    AppExitHandler();
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                    return;
            }
        }

        InitializeAppTheme();
    }

    public void InitOnAppLaunch()
    {
        Logger.Initialize();
        ImageLoader.Initialize(Path.Combine(StorageLocation.LocalCacheFolderPath, "image_cache"), clear: false);
        FocusTracker.Initialize();
        PluginManager.Instance.LoadPlugins();
    }

    private bool TryRegisterFirstInstance()
    {
        string lockFileDirPath = StorageLocation.TemporaryFolderPath;
        Directory.CreateDirectory(lockFileDirPath);
        string lockFilePath = Path.Combine(lockFileDirPath, "app.lock");
        bool lockFileExists = File.Exists(lockFilePath);

        try
        {
            var fileStream = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            fileStream.Lock(0, 0);
            _appLock = fileStream;
        }
        catch (IOException)
        {
            return false;
        }

        ExitedNormallyLastTime = !lockFileExists;
        return true;
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
