// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReader.SDK.Database.Misc;

namespace ComicReader.SDK.Common.DebugTools;

public class DebugSwitchModel : JsonDatabase<DebugSwitchModel.JsonModel>
{
    public static readonly DebugSwitchModel Instance = new();

    private JsonModel? _config;
    private List<LogTag?>? _consoleWhitelist;

    private readonly JsonSerializerOptions _serializeOption = new()
    {
        WriteIndented = true,
    };

    public bool ConsoleEnabled
    {
        get => DebugUtils.DeveloperMode && GetConfig().ConsoleEnabled;
        set
        {
            JsonModel model = GetConfig();
            model.ConsoleEnabled = value;
            UpdateModel(model);
        }
    }

    public bool LogTreeEnabled
    {
        get => DebugUtils.DeveloperMode && GetConfig().LogTreeEnabled;
        set
        {
            JsonModel model = GetConfig();
            model.LogTreeEnabled = value;
            UpdateModel(model);
        }
    }

    public List<LogTag?> ConsoleWhitelist
    {
        get
        {
            List<LogTag?>? tags = _consoleWhitelist;
            if (tags is not null)
            {
                return tags;
            }

            List<string?> tagsJson = GetConfig().ConsoleWhitelist ?? [null];
            tags = new(tagsJson.Count);
            foreach (string? tagJson in tagsJson)
            {
                var tag = LogTag.FromString(tagJson ?? string.Empty);
                tags.Add(tag);
            }

            _consoleWhitelist = tags;
            return tags;
        }
    }

    public string? DonorStoreId => DebugUtils.DeveloperMode ? GetConfig().DonorStoreId : null;

    private DebugSwitchModel() : base("debug.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    internal void Initialize()
    {
        JsonModel model = Read((m) => m);
        UpdateConfig(model);
    }

    public string GetConfigAsJson()
    {
        return JsonSerializer.Serialize(GetConfig(), _serializeOption);
    }

    public void SaveConfigFromJson(string json)
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
        model.ConsoleWhitelist?.Sort();

        _config = model;
        InvalidateCache();
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
        public bool ConsoleEnabled { get; set; } = false;

        [JsonPropertyName("ConsoleWhitelist")]
        public List<string?>? ConsoleWhitelist { get; set; } = null;

        [JsonPropertyName("LogTreeEnabled")]
        public bool LogTreeEnabled { get; set; } = false;

        [JsonPropertyName("DonorStoreId")]
        public string? DonorStoreId { get; set; } = null;
    }
}
