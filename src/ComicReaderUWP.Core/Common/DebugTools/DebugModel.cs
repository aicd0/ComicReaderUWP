// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReaderUWP.Core.Common.Constants;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.Misc;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class DebugModel
{
    private const string TAG = nameof(DebugModel);
    private const string KEY_WAIT_FOR_DEBUGGER = "WaitForDebugger";
    private const string KEY_CONSOLE_ENABLED = "ConsoleEnabled";
    private const string KEY_LOG_TREE_WHITELIST = "LogTreeWhitelist";

    private static readonly string[] _logTreeBaseTags = [];

    private static readonly Lock _lock = new();
    private static volatile JsonModel? _jsonModel;

    private static readonly JsonSerializerOptions _serializerOption = new()
    {
        WriteIndented = true,
    };

    public static string LoadJsonConfig()
    {
        return JsonSerializer.Serialize(GetJsonModel(), _serializerOption);
    }

    public static void SaveJsonConfig(string json)
    {
        JsonModel jsonModel = JsonSerializer.Deserialize<JsonModel>(json) ?? new();
        string logTreeWhitelistJson = JsonSerializer.Serialize(jsonModel.LogTreeWhitelist);
        lock (_lock)
        {
            _jsonModel = jsonModel;

            IRegistryKey registryKey = GetRegistryKey();
            registryKey.Set(KEY_WAIT_FOR_DEBUGGER, jsonModel.WaitForDebugger);
            registryKey.Set(KEY_CONSOLE_ENABLED, jsonModel.ConsoleEnabled);
            registryKey.Set(KEY_LOG_TREE_WHITELIST, logTreeWhitelistJson);
        }
    }

    public static bool WaitForDebugger
    {
        get => DebugUtils.DeveloperMode && GetJsonModel().WaitForDebugger;
    }

    public static bool ConsoleEnabled
    {
        get => DebugUtils.DeveloperMode && GetJsonModel().ConsoleEnabled;
    }

    public static bool IsLogTreeWhitelisted(string tag)
    {
        foreach (string baseTag in _logTreeBaseTags)
        {
            if (Logger.IsTagMatch(baseTag, tag))
            {
                return true;
            }
        }

        List<string?>? whitelist = GetJsonModel().LogTreeWhitelist;
        if (whitelist is null)
        {
            return false;
        }

        foreach (string? entry in whitelist)
        {
            if (entry is null || Logger.IsTagMatch(entry, tag))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonModel GetJsonModel()
    {
        if (_jsonModel is not null)
        {
            return _jsonModel;
        }

        lock (_lock)
        {
            if (_jsonModel is not null)
            {
                return _jsonModel;
            }

            IRegistryKey registryKey = GetRegistryKey();

            string logTreeWhitelistJson = registryKey.GetValueOrDefault(KEY_LOG_TREE_WHITELIST, string.Empty);
            List<string?>? logTreeWhitelist = null;
            if (!string.IsNullOrEmpty(logTreeWhitelistJson))
            {
                try
                {
                    logTreeWhitelist = JsonSerializer.Deserialize<List<string?>>(logTreeWhitelistJson);
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                }
            }

            _jsonModel = new JsonModel
            {
                WaitForDebugger = registryKey.GetValueOrDefault(KEY_WAIT_FOR_DEBUGGER, false),
                ConsoleEnabled = registryKey.GetValueOrDefault(KEY_CONSOLE_ENABLED, false),
                LogTreeWhitelist = logTreeWhitelist,
            };

            return _jsonModel;
        }
    }

    private static IRegistryKey GetRegistryKey()
    {
        return CoreDB.CoreRegistry.CreateKey(RegistryNames.DEBUG_SETTINGS);
    }

    public class JsonModel
    {
        [JsonPropertyName("WaitForDebugger")]
        public bool WaitForDebugger { get; set; }

        [JsonPropertyName("ConsoleEnabled")]
        public bool ConsoleEnabled { get; set; }

        [JsonPropertyName("LogTreeWhitelist")]
        public List<string?>? LogTreeWhitelist { get; set; }
    }
}
