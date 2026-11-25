// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Property;

namespace ComicReaderUWP.Data.Models.MediaProperty;

internal class ComicPropertyModel
{
    private const string TAG = nameof(ComicPropertyModel);
    private const string PROP_TYPE_TITLE = "Title";
    private const string PROP_TYPE_PROGRESS = "Progress";
    private const string PROP_TYPE_TAG = "Tag";
    private const string PROP_TYPE_RATING = "Rating";
    private const string PROP_TYPE_COMPLETION_STATUS = "CompletionState";
    private const string PROP_TYPE_LAST_READ_TIME = "LastReadTime";
    private const string PROP_TYPE_LAST_READ_TIME_BY_YEAR = "LastReadTimeByYear";
    private const string PROP_TYPE_LAST_READ_TIME_BY_SEASON = "LastReadTimeBySeason";
    private const string PROP_TYPE_LAST_READ_TIME_BY_MONTH = "LastReadTimeByMonth";
    private const string PROP_TYPE_PAGES = "Pages";
    private const string PROP_TYPE_RELEASE_DATE = "ReleaseDate";
    private const string PROP_TYPE_RELEASE_DATE_BY_YEAR = "ReleaseDateByYear";
    private const string PROP_TYPE_RELEASE_DATE_BY_SEASON = "ReleaseDateBySeason";
    private const string PROP_TYPE_RELEASE_DATE_BY_MONTH = "ReleaseDateByMonth";
    private const string PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY = "PluginVirtualProperty";

    private static readonly List<PropertyTypeEnum> _properties = [
        PropertyTypeEnum.Title,
        PropertyTypeEnum.Progress,
        PropertyTypeEnum.Rating,
        PropertyTypeEnum.CompletionStatus,
        PropertyTypeEnum.LastReadTime,
        PropertyTypeEnum.LastReadTimeByYear,
        PropertyTypeEnum.LastReadTimeBySeason,
        PropertyTypeEnum.LastReadTimeByMonth,
        PropertyTypeEnum.Pages,
        PropertyTypeEnum.ReleaseDate,
        PropertyTypeEnum.ReleaseDateByYear,
        PropertyTypeEnum.ReleaseDateBySeason,
        PropertyTypeEnum.ReleaseDateByMonth,
    ];

    public static async Task<List<ComicPropertyModel>> GetProperties()
    {
        var properties = new List<ComicPropertyModel>();
        foreach (PropertyTypeEnum propertyType in _properties)
        {
            properties.Add(new ComicPropertyModel
            {
                Type = propertyType,
            });
        }

        {
            List<string> tagCategories = await ComicModel.GetAllTagCategories();
            tagCategories.Sort((a, b) => string.Compare(a, b));
            foreach (string tag in tagCategories)
            {
                properties.Add(new ComicPropertyModel
                {
                    Type = PropertyTypeEnum.Tag,
                    Name = tag,
                });
            }
        }

        IEnumerable<PluginContext> plugins = PluginManager.Instance.GetActivePlugins();
        foreach (PluginContext plugin in plugins)
        {
            string pluginName = plugin.Name;
            IEnumerable<IVirtualProperty<IComicModel>> pluginProperties = plugin.GetAllComicVirtualProperties();
            foreach (IVirtualProperty<IComicModel> property in pluginProperties)
            {
                properties.Add(new ComicPropertyModel
                {
                    Type = PropertyTypeEnum.PluginVirtualProperty,
                    Name = $"{pluginName}:{property.Name}",
                    DisplayName = property.DisplayName,
                });
            }
        }

        return properties;
    }

    public static ComicPropertyModel? FromSortJson(JsonNode? model)
    {
        if (model == null)
        {
            return null;
        }

        JsonModel? jsonModel = null;
        try
        {
            jsonModel = JsonSerializer.Deserialize<JsonModel>(model);
        }
        catch (JsonException ex)
        {
            Logger.F(TAG, nameof(FromSortJson), ex);
        }
        if (jsonModel == null)
        {
            return null;
        }

        return new ComicPropertyModel
        {
            Type = StringToPropertyType(jsonModel.Type),
            Name = jsonModel.Name ?? "",
        };
    }

    public static ComicPropertyModel? FromGroupJson(JsonNode? model)
    {
        ComicPropertyModel? property = FromSortJson(model);
        return property?.Type switch
        {
            PropertyTypeEnum.LastReadTime => new ComicPropertyModel { Type = PropertyTypeEnum.LastReadTimeByYear },
            PropertyTypeEnum.ReleaseDate => new ComicPropertyModel { Type = PropertyTypeEnum.ReleaseDateByYear },
            _ => property,
        };
    }

    private static string PropertyTypeToString(PropertyTypeEnum value)
    {
        return value switch
        {
            PropertyTypeEnum.Title => PROP_TYPE_TITLE,
            PropertyTypeEnum.Progress => PROP_TYPE_PROGRESS,
            PropertyTypeEnum.Tag => PROP_TYPE_TAG,
            PropertyTypeEnum.Rating => PROP_TYPE_RATING,
            PropertyTypeEnum.CompletionStatus => PROP_TYPE_COMPLETION_STATUS,
            PropertyTypeEnum.LastReadTime => PROP_TYPE_LAST_READ_TIME,
            PropertyTypeEnum.LastReadTimeByYear => PROP_TYPE_LAST_READ_TIME_BY_YEAR,
            PropertyTypeEnum.LastReadTimeBySeason => PROP_TYPE_LAST_READ_TIME_BY_SEASON,
            PropertyTypeEnum.LastReadTimeByMonth => PROP_TYPE_LAST_READ_TIME_BY_MONTH,
            PropertyTypeEnum.Pages => PROP_TYPE_PAGES,
            PropertyTypeEnum.ReleaseDate => PROP_TYPE_RELEASE_DATE,
            PropertyTypeEnum.ReleaseDateByYear => PROP_TYPE_RELEASE_DATE_BY_YEAR,
            PropertyTypeEnum.ReleaseDateBySeason => PROP_TYPE_RELEASE_DATE_BY_SEASON,
            PropertyTypeEnum.ReleaseDateByMonth => PROP_TYPE_RELEASE_DATE_BY_MONTH,
            PropertyTypeEnum.PluginVirtualProperty => PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY,
            _ => PROP_TYPE_TITLE,
        };
    }

    private static PropertyTypeEnum StringToPropertyType(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return PropertyTypeEnum.Title;
        }

        return value switch
        {
            PROP_TYPE_TITLE => PropertyTypeEnum.Title,
            PROP_TYPE_PROGRESS => PropertyTypeEnum.Progress,
            PROP_TYPE_TAG => PropertyTypeEnum.Tag,
            PROP_TYPE_RATING => PropertyTypeEnum.Rating,
            PROP_TYPE_COMPLETION_STATUS => PropertyTypeEnum.CompletionStatus,
            PROP_TYPE_LAST_READ_TIME => PropertyTypeEnum.LastReadTime,
            PROP_TYPE_LAST_READ_TIME_BY_YEAR => PropertyTypeEnum.LastReadTimeByYear,
            PROP_TYPE_LAST_READ_TIME_BY_SEASON => PropertyTypeEnum.LastReadTimeBySeason,
            PROP_TYPE_LAST_READ_TIME_BY_MONTH => PropertyTypeEnum.LastReadTimeByMonth,
            PROP_TYPE_PAGES => PropertyTypeEnum.Pages,
            PROP_TYPE_RELEASE_DATE => PropertyTypeEnum.ReleaseDate,
            PROP_TYPE_RELEASE_DATE_BY_YEAR => PropertyTypeEnum.ReleaseDateByYear,
            PROP_TYPE_RELEASE_DATE_BY_SEASON => PropertyTypeEnum.ReleaseDateBySeason,
            PROP_TYPE_RELEASE_DATE_BY_MONTH => PropertyTypeEnum.ReleaseDateByMonth,
            PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY => PropertyTypeEnum.PluginVirtualProperty,
            _ => PropertyTypeEnum.Title,
        };
    }

    private static bool IsDateVariant(PropertyTypeEnum type)
    {
        return type is PropertyTypeEnum.LastReadTimeByYear
            or PropertyTypeEnum.LastReadTimeBySeason
            or PropertyTypeEnum.LastReadTimeByMonth
            or PropertyTypeEnum.ReleaseDateByYear
            or PropertyTypeEnum.ReleaseDateBySeason
            or PropertyTypeEnum.ReleaseDateByMonth;
    }

    public PropertyTypeEnum Type { get; init; } = PropertyTypeEnum.Title;

    public string Name { get; init; } = string.Empty;

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_displayName))
            {
                return _displayName;
            }

            return Type switch
            {
                PropertyTypeEnum.Title => StringResourceProvider.Instance.Title,
                PropertyTypeEnum.Progress => StringResourceProvider.Instance.Progress,
                PropertyTypeEnum.Tag => Name,
                PropertyTypeEnum.Rating => StringResourceProvider.Instance.Rating,
                PropertyTypeEnum.CompletionStatus => StringResourceProvider.Instance.CompletionStatus,
                PropertyTypeEnum.LastReadTime => StringResourceProvider.Instance.LastReadTime,
                PropertyTypeEnum.LastReadTimeByYear => StringResourceProvider.Instance.PropertyUnitYear,
                PropertyTypeEnum.LastReadTimeBySeason => StringResourceProvider.Instance.PropertyUnitQuarter,
                PropertyTypeEnum.LastReadTimeByMonth => StringResourceProvider.Instance.PropertyUnitMonth,
                PropertyTypeEnum.Pages => StringResourceProvider.Instance.PageCount,
                PropertyTypeEnum.ReleaseDate => StringResourceProvider.Instance.ReleaseDate,
                PropertyTypeEnum.ReleaseDateByYear => StringResourceProvider.Instance.PropertyUnitYear,
                PropertyTypeEnum.ReleaseDateBySeason => StringResourceProvider.Instance.PropertyUnitQuarter,
                PropertyTypeEnum.ReleaseDateByMonth => StringResourceProvider.Instance.PropertyUnitMonth,
                _ => string.Empty,
            };
        }
        set => _displayName = value;
    }

    public string DisplayGroupName => Type switch
    {
        PropertyTypeEnum.Tag => StringResourceProvider.Instance.Tag,
        PropertyTypeEnum.LastReadTimeByYear or PropertyTypeEnum.LastReadTimeBySeason or PropertyTypeEnum.LastReadTimeByMonth => StringResourceProvider.Instance.LastReadTime,
        PropertyTypeEnum.ReleaseDateByYear or PropertyTypeEnum.ReleaseDateBySeason or PropertyTypeEnum.ReleaseDateByMonth => StringResourceProvider.Instance.ReleaseDate,
        _ => string.Empty,
    };

    public string DisplayFullName => IsDateVariant(Type)
        ? StringResourceProvider.Instance.WithParentheses(DisplayGroupName, DisplayName)
        : DisplayName;

    public bool CanSort => GetUsage(Type).HasFlag(PropertyUsageEnum.Sort);

    public bool CanGroup => GetUsage(Type).HasFlag(PropertyUsageEnum.Group);

    public bool CanAggregate => GetUsage(Type).HasFlag(PropertyUsageEnum.Aggregate);

    private bool _pluginPropertyInitialized = false;
    private IVirtualProperty<IComicModel>? _pluginProperty;
    internal IVirtualProperty<IComicModel>? PluginProperty
    {
        get
        {
            if (_pluginPropertyInitialized)
            {
                return _pluginProperty;
            }

            _pluginPropertyInitialized = true;
            if (Type != PropertyTypeEnum.PluginVirtualProperty)
            {
                return null;
            }

            string[] pieces = Name.Split(':');
            if (pieces.Length != 2)
            {
                return null;
            }

            string pluginName = pieces[0];
            string propertyName = pieces[1];
            PluginContext? plugin = PluginManager.Instance.GetActivePlugin(pluginName);
            if (plugin is null)
            {
                return null;
            }

            _pluginProperty = plugin.GetAllComicVirtualProperties().FirstOrDefault(x => x.Name == propertyName);
            return _pluginProperty;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is ComicPropertyModel other)
        {
            return Type == other.Type && Name == other.Name;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
    }

    public JsonNode? ToJson()
    {
        var jsonModel = new JsonModel
        {
            Type = PropertyTypeToString(Type),
            Name = Name
        };
        return JsonSerializer.SerializeToNode(jsonModel);
    }

    [Flags]
    internal enum PropertyUsageEnum
    {
        None = 0,
        Sort = 1,
        Group = 2,
        Aggregate = 4,
        All = Sort | Group | Aggregate,
    }

    internal static PropertyUsageEnum GetUsage(PropertyTypeEnum value)
    {
        return value switch
        {
            // Base date properties keep full precision, so they are sort and aggregate keys only.
            PropertyTypeEnum.LastReadTime => PropertyUsageEnum.Sort | PropertyUsageEnum.Aggregate,
            PropertyTypeEnum.ReleaseDate => PropertyUsageEnum.Sort | PropertyUsageEnum.Aggregate,
            // The date variants exist to offer grouping granularity.
            _ when IsDateVariant(value) => PropertyUsageEnum.Group,
            _ => PropertyUsageEnum.All,
        };
    }

    internal enum PropertyTypeEnum
    {
        CompletionStatus,
        Progress,
        Rating,
        Tag,
        Title,
        LastReadTime,
        LastReadTimeByYear,
        LastReadTimeBySeason,
        LastReadTimeByMonth,
        Pages,
        ReleaseDate,
        ReleaseDateByYear,
        ReleaseDateBySeason,
        ReleaseDateByMonth,
        PluginVirtualProperty,
    }

    private class JsonModel
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }
}
