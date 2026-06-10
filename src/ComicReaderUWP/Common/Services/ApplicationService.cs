// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.InitTask;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;

using Windows.Storage;

namespace ComicReaderUWP.Common.Services;

internal class ApplicationService : IApplicationService
{
    private const string TAG = nameof(ApplicationService);

#if PORTABLE
    private const bool PORTABLE = true;
#else
    private const bool PORTABLE = false;
#endif

    private const string DIR_USER = "user";
    private const string CONFIG_FILE = "config.json";

#pragma warning disable CS0162 // Unreachable code detected
    private static readonly Lazy<string> _configFilePath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, CONFIG_FILE);
        }
        else
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, CONFIG_FILE);
        }
    });

    private static readonly Lazy<string> _localFolderPath = new(() =>
    {
        string? configPath = GetConfig().LocalFolderPath;
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local");
        }
        else
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
    });

    private static readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        string? configPath = GetConfig().LocalCacheFolderPath;
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local_cache");
        }
        else
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
    });

    private static readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        string? configPath = GetConfig().TemporaryFolderPath;
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "temporary");
        }
        else
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
    });
#pragma warning restore CS0162 // Unreachable code detected

    private static readonly object _configLock = new();
    private static ConfigJsonModel? _config;
    private static bool _launching = true;
    private static bool _exiting = false;

    public static void StopLaunching()
    {
        _launching = false;
    }

    public static void StartExiting()
    {
        _launching = false;
        _exiting = true;
        App.Instance.WindowManager.LockWindowStatus();
    }

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }

    private static ConfigJsonModel GetConfig()
    {
        ConfigJsonModel? config = _config;
        if (config is not null)
        {
            return config;
        }

        lock (_configLock)
        {
            config = _config;
            if (config is not null)
            {
                return config;
            }

            string configFilePath = _configFilePath.Value;
            string? configText = null;
            if (File.Exists(configFilePath))
            {
                try
                {
                    configText = File.ReadAllText(configFilePath);
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                }
            }

            if (!string.IsNullOrEmpty(configText))
            {
                try
                {
                    config = JsonSerializer.Deserialize<ConfigJsonModel>(configText);
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                }
            }

            config ??= new();
            _config = config;
            return config;
        }
    }

    public bool PortableBuild => PORTABLE;

    public bool SafeMode => InitTaskManager.Instance.SafeMode;

    public bool Launching => _launching;

    public bool Exiting => _exiting;

    public string GetLocalFolderPath()
    {
        return _localFolderPath.Value;
    }

    public string GetLocalCacheFolderPath()
    {
        return _localCacheFolderPath.Value;
    }

    public string GetTemporaryFolderPath()
    {
        return _temporaryFolderPath.Value;
    }

    public string GetEnvironmentDebugInfo()
    {
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        return sb.ToString();
    }

    private class ConfigJsonModel
    {
        [JsonPropertyName("LocalFolderPath")]
        public string? LocalFolderPath { get; set; }

        [JsonPropertyName("LocalCacheFolderPath")]
        public string? LocalCacheFolderPath { get; set; }

        [JsonPropertyName("TemporaryFolderPath")]
        public string? TemporaryFolderPath { get; set; }
    }
}
