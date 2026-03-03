// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

using ComicReaderUWP.SDK.Common.Constants;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.ServiceManagement;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.Misc;
using ComicReaderUWP.SDK.Plugins;

using Windows.ApplicationModel;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace ComicReaderUWP.SDK.Common.AppEnvironment;

public class EnvironmentProvider
{
    private const string TAG = nameof(EnvironmentProvider);

    public static EnvironmentProvider Instance { get; } = new();

    private readonly object _lock = new();
    private string _additionalDebugInformation = string.Empty;
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
    public void Initialize(string additionalDebugInformation)
    {
        _additionalDebugInformation = additionalDebugInformation;
        TaskDispatcher.DefaultThreadPool.Submit("EnvironmentProviderInit", () =>
        {
            string deviceId = RecalculateDeviceId();
            _actualDeviceId = deviceId;
            SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_ACTUAL_DEVICE_ID, deviceId);
        });
    }

    public void AppendDebugText(StringBuilder sb)
    {
        sb.SafeAppend("Awake time", () => GetAwakeTime());
        sb.SafeAppend("Build type", () => DebugUtils.DebugBuild ? "Debug" : "Release");
        sb.SafeAppend("Current app language", GetCurrentAppLanguage);
        sb.SafeAppend("Current system language", GetCurrentSystemLanguage);
        sb.SafeAppend("Developer ID", GetDeveloperId);
        sb.SafeAppend("Device ID", GetDeviceId);
        sb.SafeAppend("Device model", DeviceInformationHelper.Instance.GetDeviceModel);
        sb.SafeAppend("Host version", GetHostVersion);
        sb.SafeAppend("Installed system language", GetInstalledSystemLanguage);
        sb.SafeAppend("Launch time", () => GetLaunchTime().ToString("yyyy/M/d HH:mm:ss.fff"));
        sb.SafeAppend("OEM name", DeviceInformationHelper.Instance.GetDeviceOemName);
        sb.SafeAppend("OS architecture", () => RuntimeInformation.OSArchitecture);
        sb.SafeAppend("OS build", DeviceInformationHelper.Instance.GetOsBuild);
        sb.SafeAppend("OS version", DeviceInformationHelper.Instance.GetOsVersion);
        sb.SafeAppend("Portable", () => IsPortable());
        sb.SafeAppend("Process architecture", () => RuntimeInformation.ProcessArchitecture);
        sb.SafeAppend("Processor count", () => Environment.ProcessorCount);
        sb.SafeAppend("SDK version", GetSDKVersion);

        if (_additionalDebugInformation.Length > 0)
        {
            sb.Append(_additionalDebugInformation);
            sb.Append('\n');
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

        deviceId = SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValue<string>(DatabaseEntry.KV_KEY_MAIN_DEVICE_ID);
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
            SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_DEVICE_ID, deviceId);
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

        deviceId = SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValue<string>(DatabaseEntry.KV_KEY_MAIN_ACTUAL_DEVICE_ID);
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
            catch (Exception e)
            {
                Logger.AssertNotReachHere("91F609C11120E95E", e);
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

    public string GetDeveloperId()
    {
        List<string> info = [];
        info.Add(GetActualDeviceId());
        info.Add(GetHostVersion());
        string combined = string.Join('-', info);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash)[..8];
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
        tags["cr-device-id"] = Instance.GetDeviceId();
        tags["cr-host-version"] = GetHostVersion();
        tags["cr-lang-app"] = Instance.GetCurrentAppLanguage();
        tags["cr-lang-current"] = GetCurrentSystemLanguage();
        tags["cr-lang-installed"] = GetInstalledSystemLanguage();
        tags["cr-portable"] = IsPortable() ? "true" : "false";
        tags["cr-sdk-version"] = GetSDKVersion();
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
        return ServiceManager.GetService<IApplicationService>().IsPortableBuild();
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
