// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ComicReaderUWP.Core.Common.Constants;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.Misc;
using ComicReaderUWP.SDK.Plugins;

using Windows.ApplicationModel;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace ComicReaderUWP.Core.Common.AppEnvironment;

public class EnvironmentProvider
{
    private const string TAG = nameof(EnvironmentProvider);
    private const string KEY_ACTUAL_DEVICE_ID = "ActualDeviceId";
    private const string KEY_DEVICE_ID = "DeviceId";

    public static EnvironmentProvider Instance { get; } = new();

    private readonly Lock _lock = new();
    private string _extraDebugFields = string.Empty;
    private string _appLanguageTag = string.Empty;
    private string _actualDeviceId = string.Empty;
    private string _deviceId = string.Empty;
    private string _hostVersion = string.Empty;
    private readonly DateTimeOffset _launchTime;
    private string _sdkVersion = string.Empty;

    private EnvironmentProvider()
    {
        _launchTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// Initialize the EnvironmentProvider instance. Some fields like launch time
    /// require the static instance to be initialized as soon as possible.
    /// </summary>
    public void Initialize(string extraDebugFields)
    {
        _extraDebugFields = extraDebugFields;
        TaskDispatcher.DefaultThreadPool.Submit(() =>
        {
            string deviceId = RecalculateDeviceId();
            _actualDeviceId = deviceId;
            CoreDB.CoreRegistry.CreateKey(RegistryNames.ENVIRONMENT_INFO).Set(KEY_ACTUAL_DEVICE_ID, deviceId);
        });
    }

    public void AppendDebugText(StringBuilder sb)
    {
        Dictionary<string, string> fields = [];

        void AddField(string category, Func<object?> func)
        {
            string value;
            try
            {
                value = func()?.ToString() ?? "[null]";
            }
            catch (Exception)
            {
                value = "[error]";
            }

            fields.TryAdd(category, value);
        }

        AddField("Awake time", () => GetAwakeTime());
        AddField("Build type", () => DebugUtils.DebugBuild ? "Debug" : "Release");
        AddField("Current app language", GetCurrentAppLanguage);
        AddField("Current system language", GetCurrentSystemLanguage);
        AddField("Device ID", GetDeviceId);
        AddField("Device model", DeviceInformationHelper.Instance.GetDeviceModel);
        AddField("Host version", GetHostVersion);
        AddField("Installed system language", GetInstalledSystemLanguage);
        AddField("Launch time", () => GetLaunchTime().ToString("yyyy/M/d HH:mm:ss.fff"));
        AddField("OEM name", DeviceInformationHelper.Instance.GetDeviceOemName);
        AddField("OS architecture", GetSystemArchitecture);
        AddField("OS build", DeviceInformationHelper.Instance.GetOsBuild);
        AddField("OS version", DeviceInformationHelper.Instance.GetOsVersion);
        AddField("Portable", () => IsPortable());
        AddField("Process architecture", GetProcessArchitecture);
        AddField("Processor count", () => Environment.ProcessorCount);
        AddField("Safe mode", () => IsSafeMode());
        AddField("SDK version", GetSDKVersion);

        if (_extraDebugFields.Length > 0)
        {
            Dictionary<string, object?> extraFields;
            try
            {
                extraFields = JsonSerializer.Deserialize<Dictionary<string, object?>>(_extraDebugFields) ?? [];
            }
            catch (Exception)
            {
                extraFields = [];
            }

            foreach (KeyValuePair<string, object?> item in extraFields)
            {
                AddField(item.Key, () => item.Value);
            }
        }

        bool first = true;
        foreach (string key in fields.Keys.Order())
        {
            if (!first)
            {
                sb.AppendLine();
            }

            sb.Append(key).Append(": ").Append(fields[key]);
            first = false;
        }
    }

    public string GetHostVersion()
    {
        if (!string.IsNullOrEmpty(_hostVersion))
        {
            return _hostVersion;
        }

        string hostVersion;
        if (IsPortable())
        {
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version == null)
            {
                return "0.0.0.0";
            }

            hostVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        else
        {
            PackageVersion version = Package.Current.Id.Version;
            hostVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        _hostVersion = hostVersion;
        return hostVersion;
    }

    public string GetSDKVersion()
    {
        if (!string.IsNullOrEmpty(_sdkVersion))
        {
            return _sdkVersion;
        }

        Version? version = Assembly.GetAssembly(typeof(IPlugin))?.GetName().Version;
        if (version is null)
        {
            return "0.0.0.0";
        }

        string sdkVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        _sdkVersion = sdkVersion;
        return sdkVersion;
    }

    public string GetDeviceId()
    {
        string? deviceId = _deviceId;
        if (!string.IsNullOrEmpty(deviceId))
        {
            return deviceId;
        }

        deviceId = CoreDB.CoreRegistry.CreateKey(RegistryNames.ENVIRONMENT_INFO).GetValue<string>(KEY_DEVICE_ID);
        if (!string.IsNullOrEmpty(deviceId))
        {
            _deviceId = deviceId;
            return deviceId;
        }

        deviceId = RecalculateDeviceId();
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_deviceId))
            {
                return _deviceId;
            }

            _deviceId = deviceId;
            CoreDB.CoreRegistry.CreateKey(RegistryNames.ENVIRONMENT_INFO).Set(KEY_DEVICE_ID, deviceId);
        }

        return deviceId;
    }

    public string GetActualDeviceId()
    {
        string? deviceId = _actualDeviceId;
        if (!string.IsNullOrEmpty(deviceId))
        {
            return deviceId;
        }

        deviceId = CoreDB.CoreRegistry.CreateKey(RegistryNames.ENVIRONMENT_INFO).GetValue<string>(KEY_ACTUAL_DEVICE_ID);
        if (!string.IsNullOrEmpty(deviceId))
        {
            _actualDeviceId = deviceId;
            return deviceId;
        }

        return GetDeviceId();
    }

    public string GetCurrentAppLanguage()
    {
        string languageTag = _appLanguageTag;
        if (!string.IsNullOrEmpty(languageTag))
        {
            return languageTag;
        }

        if (!IsPortable())
        {
            try
            {
                languageTag = ApplicationLanguages.PrimaryLanguageOverride;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
            }

            if (!string.IsNullOrEmpty(languageTag))
            {
                _appLanguageTag = languageTag;
                return languageTag;
            }
        }

        return CultureInfo.CurrentUICulture.Name;
    }

    public void SetCurrentAppLanguage(string languageTag)
    {
        _appLanguageTag = languageTag;
    }

    public CultureInfo GetCurrentAppLanguageInfo()
    {
        string languageTag = GetCurrentAppLanguage();
        if (string.IsNullOrEmpty(languageTag))
        {
            return CultureInfo.CurrentUICulture;
        }

        try
        {
            return new CultureInfo(languageTag);
        }
        catch (CultureNotFoundException)
        {
            Logger.F(TAG, $"Culture '{languageTag}' not found");
            return CultureInfo.CurrentUICulture;
        }
    }

    public DateTimeOffset GetLaunchTime()
    {
        return _launchTime;
    }

    public TimeSpan GetAwakeTime()
    {
        return DateTimeOffset.Now - _launchTime;
    }

    public Dictionary<string, string> GetEnvironmentTags()
    {
        Dictionary<string, string> tags = [];
        tags["c-arch-os"] = GetSystemArchitecture();
        tags["c-arch-process"] = GetProcessArchitecture();
        tags["c-debug-mode"] = DebugUtils.DebugMode ? "true" : "false";
        tags["c-device-id"] = Instance.GetDeviceId();
        tags["c-host-version"] = GetHostVersion();
        tags["c-lang-app"] = Instance.GetCurrentAppLanguage();
        tags["c-lang-os-current"] = GetCurrentSystemLanguage();
        tags["c-lang-os-installed"] = GetInstalledSystemLanguage();
        tags["c-portable"] = IsPortable() ? "true" : "false";
        tags["c-safe-mode"] = IsSafeMode() ? "true" : "false";
        tags["c-sdk-version"] = GetSDKVersion();
        return tags;
    }

    public static string GetInstalledSystemLanguage()
    {
        return CultureInfo.InstalledUICulture.Name;
    }

    public static string GetCurrentSystemLanguage()
    {
        return GlobalizationPreferences.Languages[0];
    }

    public static bool IsPortable()
    {
        return ServiceManager.GetService<IApplicationService>().PortableBuild;
    }

    public static bool IsSafeMode()
    {
        return ServiceManager.GetService<IApplicationService>().SafeMode;
    }

    private static string GetSystemArchitecture()
    {
        return RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
    }

    private static string GetProcessArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    }

    private static string RecalculateDeviceId()
    {
        string combined = string.Join('-', GetDeviceList());
        if (combined.Length < 12)
        {
            combined = Guid.NewGuid().ToString();
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash)[..16];
    }

    private static List<string> GetDeviceList()
    {
        List<string> results = [];

        string? cpuId = DeviceInformationHelper.Instance.GetCpuId();
        if (!string.IsNullOrEmpty(cpuId))
        {
            results.Add(cpuId);
        }

        string[] macAddresses = [.. NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .Where(mac => !string.IsNullOrEmpty(mac))];
        results.AddRange(macAddresses);

        string? motherboardSerial = DeviceInformationHelper.Instance.GetMotherboardSerial();
        if (!string.IsNullOrEmpty(motherboardSerial))
        {
            results.Add(motherboardSerial);
        }

        return results;
    }
}
