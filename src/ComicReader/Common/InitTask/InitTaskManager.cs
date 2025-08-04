// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.Services;
using ComicReader.Data;
using ComicReader.Data.Legacy;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.ServiceManagement;

using Microsoft.UI.Xaml;
using Microsoft.Windows.Globalization;

namespace ComicReader.Common.InitTask;

internal class InitTaskManager(Application application)
{
    private readonly Application _application = application;

    public void InitOnAppCreate()
    {
        FailFastOnException(InitOnAppCreateInternal);
    }

    public void InitOnAppLaunch()
    {
        FailFastOnException(InitOnAppLaunchInternal);
    }

    private void InitOnAppCreateInternal()
    {
        // Register crash handler
        _application.UnhandledException += (_, e) =>
        {
            DebugUtils.CaptureFatalError(e.Exception);
        };

        // Register services
        ServiceManager.RegisterService<IApplicationService>(new ApplicationService());
        ServiceManager.RegisterService<IDebugService>(new DebugService());

        // Initialize environment information
        EnvironmentProvider.Instance.Initialize(Properties.AdditionalDebugInformation);

        // Initialize Sentry
        SentryManager.Initialize(Properties.SentryDsn, EnvironmentProvider.GetEnvironmentTags());

        // Initialize app language
        InitializeAppLanguage();

        // Initialize app theme
        InitializeAppTheme();
    }

    private void InitOnAppLaunchInternal()
    {
        // Initialize debug tools
        DebugUtils.Initialize();

        // Initialize logger
        Logger.Initialize();

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

    private void FailFastOnException(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            DebugUtils.CaptureFatalError(e);
            Environment.FailFast("A fatal error occurred during startup.", e);
            throw;
        }
    }
}
