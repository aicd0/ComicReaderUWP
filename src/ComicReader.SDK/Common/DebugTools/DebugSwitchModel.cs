// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReader.SDK.Data;

namespace ComicReader.SDK.Common.DebugTools;

internal class DebugSwitchModel : JsonDatabase<DebugSwitchModel.JsonModel>
{
    public static readonly DebugSwitchModel Instance = new();

    private JsonModel? _config;
    private LogTag? _consoleWhitelist;

    private readonly JsonSerializerOptions _serializeOption = new()
    {
        WriteIndented = true,
    };

    private bool ConsoleEnabled
    {
        get => DebugUtils.DeveloperMode && GetConfig().ConsoleEnabled;
        set
        {
            JsonModel model = GetConfig();
            model.ConsoleEnabled = value;
            UpdateModel(model);
        }
    }

    private bool LogTreeEnabled
    {
        get => DebugUtils.DeveloperMode && GetConfig().LogTreeEnabled;
        set
        {
            JsonModel model = GetConfig();
            model.LogTreeEnabled = value;
            UpdateModel(model);
        }
    }

    public bool SqliteLogEnabled
    {
        get => DebugUtils.DebugMode && GetConfig().SqliteLogEnabled;
    }

    private LogTag? ConsoleWhitelist
    {
        get
        {
            LogTag? tag = _consoleWhitelist;
            if (tag != null)
            {
                return tag;
            }
            JsonObject? jsonObject = GetConfig().ConsoleWhitelist;
            if (jsonObject == null)
            {
                return null;
            }
            tag = LogTag.FromJson(jsonObject);
            _consoleWhitelist = tag;
            return tag;
        }
    }

    private DebugSwitchModel() : base("debug.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public void Initialize()
    {
        JsonModel model = Read((m) => m);
        UpdateConfig(model);
    }

    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(GetConfig(), _serializeOption);
    }

    public void SaveConfig(string json)
    {
        JsonModel config = JsonSerializer.Deserialize<JsonModel>(json) ?? new();
        UpdateConfig(config);
        UpdateModel(config);
    }

    private JsonModel GetConfig()
    {
        JsonModel? config = _config;
        if (config != null)
        {
            return config;
        }
        config = new();
        UpdateConfig(config);
        return config;
    }

    private void UpdateConfig(JsonModel model)
    {
        _config = model;
        InvalidateCache();
        Logger.SetConsoleEnabled(ConsoleEnabled);
        Logger.SetConsoleWhitelist(ConsoleWhitelist);
        Logger.SetLogTreeEnabled(LogTreeEnabled);
    }

    private void InvalidateCache()
    {
        _consoleWhitelist = null;
    }

    private void UpdateModel(JsonModel model)
    {
        Write(model);
        Save();
    }

    public class JsonModel
    {
        [JsonPropertyName("ConsoleEnabled")]
        public bool ConsoleEnabled { get; set; }

        [JsonPropertyName("ConsoleWhitelist")]
        public JsonObject? ConsoleWhitelist { get; set; }

        [JsonPropertyName("LogTreeEnabled")]
        public bool LogTreeEnabled { get; set; }

        [JsonPropertyName("SqliteLogEnabled")]
        public bool SqliteLogEnabled { get; set; }
    }
}
