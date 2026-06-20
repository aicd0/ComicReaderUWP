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
    private const string KEY_LOG_TREE_ENABLED = "LogTreeEnabled";
    private const string KEY_CONSOLE_WHITELIST = "ConsoleWhitelist";
    private const string KEY_DONOR_STORE_ID = "DonorStoreId";

    private static readonly object _lock = new();
    private static JsonModel? _jsonModel;
    private static List<LogTag?>? _consoleWhitelist;

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
        string consoleWhitelistJson = JsonSerializer.Serialize(jsonModel.ConsoleWhitelist);
        lock (_lock)
        {
            _jsonModel = jsonModel;
            _consoleWhitelist = null;

            IRegistryKey registryKey = GetRegistryKey();
            registryKey.Set(KEY_WAIT_FOR_DEBUGGER, jsonModel.WaitForDebugger);
            registryKey.Set(KEY_CONSOLE_ENABLED, jsonModel.ConsoleEnabled);
            registryKey.Set(KEY_CONSOLE_WHITELIST, consoleWhitelistJson);
            registryKey.Set(KEY_LOG_TREE_ENABLED, jsonModel.LogTreeEnabled);
            registryKey.Set(KEY_DONOR_STORE_ID, jsonModel.DonorStoreId);
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

    public static bool LogTreeEnabled
    {
        get => DebugUtils.DeveloperMode && GetJsonModel().LogTreeEnabled;
    }

    public static List<LogTag?> ConsoleWhitelist
    {
        get
        {
            if (_consoleWhitelist is not null)
            {
                return _consoleWhitelist;
            }

            List<string?> tagList = GetJsonModel().ConsoleWhitelist ?? [null];
            List<LogTag?> tags = new(tagList.Count);
            foreach (string? item in tagList)
            {
                var tag = LogTag.FromString(item ?? string.Empty);
                tags.Add(tag);
            }

            _consoleWhitelist = tags;
            return tags;
        }
    }

    public static string? DonorStoreId
    {
        get
        {
            if (DebugUtils.DeveloperMode)
            {
                return null;
            }

            string id = GetJsonModel().DonorStoreId;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return id;
        }
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

            string consoleWhitelistJson = registryKey.GetValueOrDefault(KEY_CONSOLE_WHITELIST, string.Empty);
            List<string?>? consoleWhitelist = null;
            if (!string.IsNullOrEmpty(consoleWhitelistJson))
            {
                try
                {
                    consoleWhitelist = JsonSerializer.Deserialize<List<string?>>(consoleWhitelistJson);
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
                ConsoleWhitelist = consoleWhitelist,
                LogTreeEnabled = registryKey.GetValueOrDefault(KEY_LOG_TREE_ENABLED, false),
                DonorStoreId = registryKey.GetValueOrDefault(KEY_DONOR_STORE_ID, string.Empty),
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

        [JsonPropertyName("ConsoleWhitelist")]
        public List<string?>? ConsoleWhitelist { get; set; }

        [JsonPropertyName("LogTreeEnabled")]
        public bool LogTreeEnabled { get; set; }

        [JsonPropertyName("DonorStoreId")]
        public string DonorStoreId { get; set; } = string.Empty;
    }
}
