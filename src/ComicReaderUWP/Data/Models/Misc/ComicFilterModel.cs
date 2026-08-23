// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Data.Models.Misc;

class ComicFilterModel : JsonDatabase<ComicFilterModel.JsonModel>
{
    public const string VIEW_TYPE_LARGE = "Large";
    public const string VIEW_TYPE_MEDIUM = "Medium";
    public const string FUNCTION_TYPE_NONE = "None";
    public const string FUNCTION_TYPE_ITEM_COUNT = "ItemCount";
    public const string FUNCTION_TYPE_MAX = "Max";
    public const string FUNCTION_TYPE_MIN = "Min";
    public const string FUNCTION_TYPE_SUM = "Sum";
    public const string FUNCTION_TYPE_AVERAGE = "Average";
    public const string ORDER_METHOD_ASCENDING = "Ascending";
    public const string ORDER_METHOD_DESCENDING = "Descending";
    public const string ORDER_METHOD_SHUFFLE = "Shuffle";
    public const string ORDER_METHOD_SHUFFLE_STABLE = "ShuffleStable";

    public static readonly ComicFilterModel Instance = new();

    private ComicFilterModel() : base("filters.json") { }

    protected override JsonModel InitializeModel(JsonModel? model)
    {
        model ??= new();
        return model;
    }

    public ExternalModel? GetModel()
    {
        return Read(ExternalModel.From);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(model.To);
        Save();
        GlobalEvent.Instance.FilterUpdated.Emit(0);
    }

    public class ExternalModel
    {
        public ExternalFilterModel? LastFilter { get; set; }
        public List<ExternalFilterModel> Filters { get; set; } = [];

        public static ExternalModel? From(JsonModel? model)
        {
            if (model == null)
            {
                return null;
            }

            List<ExternalFilterModel> filters = [];
            if (model.Filters != null)
            {
                foreach (FilterModel? filter in model.Filters)
                {
                    ExternalFilterModel? externalFilter = filter is null ? null : ExternalFilterModel.From(filter);
                    if (externalFilter is not null)
                    {
                        filters.Add(externalFilter);
                    }
                }
            }

            return new ExternalModel
            {
                LastFilter = model.LastFilter is null ? null : ExternalFilterModel.From(model.LastFilter),
                Filters = filters
            };
        }

        public void To(JsonModel model)
        {
            model.LastFilter = LastFilter?.To();
            model.Filters = Filters?.ConvertAll(x => x?.To()) ?? [];
        }
    }

    public class ExternalFilterModel
    {
        public string Name { get; set; } = string.Empty;
        public bool Modified { get; set; } = false;
        public ComicPropertyModel SortBy { get; set; } = new();
        public OrderMethodEnum ComicOrderMethod { get; set; } = OrderMethodEnum.Ascending;
        public ComicPropertyModel? GroupBy { get; set; } = null;
        public OrderMethodEnum GroupOrderMethod { get; set; } = OrderMethodEnum.Ascending;
        public FunctionTypeEnum GroupSortingFunction { get; set; } = FunctionTypeEnum.None;
        public ComicPropertyModel? GroupSortingProperty { get; set; } = null;
        public IReadOnlySet<string> CollapsedGroups { get; set; } = FrozenSet<string>.Empty;
        public ViewTypeEnum ViewType { get; set; } = ViewTypeEnum.Large;
        public bool IncludeHiddenComics { get; set; } = false;
        public bool SaveViewSettings { get; set; } = false;
        public bool SaveSortingAndGroupingSettings { get; set; } = true;
        public string Expression { get; set; } = string.Empty;

        private ExternalFilterModel() { }

        public ExternalFilterModel Clone()
        {
            return From(To());
        }

        public FilterModel To()
        {
            return new FilterModel
            {
                Name = Name,
                Modified = Modified,
                SortBy = SortBy.ToJson(),
                ComicOrderMethod = OrderMethodToString(ComicOrderMethod),
                GroupBy = GroupBy?.ToJson(),
                GroupOrderMethod = OrderMethodToString(GroupOrderMethod),
                GroupSortingFunction = FunctionTypeToString(GroupSortingFunction),
                GroupSortingProperty = GroupSortingProperty?.ToJson(),
                CollapsedGroups = [.. CollapsedGroups],
                ViewType = ViewTypeToString(ViewType),
                IncludeHiddenComics = IncludeHiddenComics,
                SaveViewSettings = SaveViewSettings,
                SaveSortingAndGroupingSettings = SaveSortingAndGroupingSettings,
                Expression = Expression,
            };
        }

        public static ExternalFilterModel From(FilterModel model)
        {
            ExternalFilterModel defaultModel = FromDefault();
            return new ExternalFilterModel
            {
                Name = model.Name ?? string.Empty,
                Modified = model.Modified ?? false,
                SortBy = ComicPropertyModel.FromJson(model.SortBy) ?? defaultModel.SortBy,
                ComicOrderMethod = string.IsNullOrEmpty(model.ComicOrderMethod) ?
                    defaultModel.ComicOrderMethod :
                    StringToOrderMethod(model.ComicOrderMethod),
                GroupBy = ComicPropertyModel.FromJson(model.GroupBy),
                GroupOrderMethod = string.IsNullOrEmpty(model.GroupOrderMethod) ?
                    defaultModel.GroupOrderMethod :
                    StringToOrderMethod(model.GroupOrderMethod),
                GroupSortingFunction = string.IsNullOrEmpty(model.GroupSortingFunction) ?
                    defaultModel.GroupSortingFunction : StringToFunctionType(model.GroupSortingFunction),
                GroupSortingProperty = ComicPropertyModel.FromJson(model.GroupSortingProperty),
                CollapsedGroups = (model.CollapsedGroups ?? []).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToFrozenSet(),
                ViewType = string.IsNullOrEmpty(model.ViewType) ?
                    defaultModel.ViewType : StringToViewType(model.ViewType),
                IncludeHiddenComics = model.IncludeHiddenComics ?? defaultModel.IncludeHiddenComics,
                SaveViewSettings = model.SaveViewSettings ?? defaultModel.SaveViewSettings,
                SaveSortingAndGroupingSettings = model.SaveSortingAndGroupingSettings ?? defaultModel.SaveSortingAndGroupingSettings,
                Expression = model.Expression ?? defaultModel.Expression,
            };
        }

        public static ExternalFilterModel FromDefault()
        {
            return new ExternalFilterModel
            {
                Name = StringResourceProvider.Instance.Default,
                Modified = false,
            };
        }

        private static string ViewTypeToString(ViewTypeEnum value)
        {
            return value switch
            {
                ViewTypeEnum.Large => VIEW_TYPE_LARGE,
                ViewTypeEnum.Medium => VIEW_TYPE_MEDIUM,
                _ => VIEW_TYPE_LARGE,
            };
        }

        private static ViewTypeEnum StringToViewType(string value)
        {
            return value switch
            {
                VIEW_TYPE_LARGE => ViewTypeEnum.Large,
                VIEW_TYPE_MEDIUM => ViewTypeEnum.Medium,
                _ => ViewTypeEnum.Large,
            };
        }

        private static string FunctionTypeToString(FunctionTypeEnum value)
        {
            return value switch
            {
                FunctionTypeEnum.None => FUNCTION_TYPE_NONE,
                FunctionTypeEnum.ItemCount => FUNCTION_TYPE_ITEM_COUNT,
                FunctionTypeEnum.Max => FUNCTION_TYPE_MAX,
                FunctionTypeEnum.Min => FUNCTION_TYPE_MIN,
                FunctionTypeEnum.Sum => FUNCTION_TYPE_SUM,
                FunctionTypeEnum.Average => FUNCTION_TYPE_AVERAGE,
                _ => FUNCTION_TYPE_NONE,
            };
        }

        private static FunctionTypeEnum StringToFunctionType(string value)
        {
            return value switch
            {
                FUNCTION_TYPE_NONE => FunctionTypeEnum.None,
                FUNCTION_TYPE_ITEM_COUNT => FunctionTypeEnum.ItemCount,
                FUNCTION_TYPE_MAX => FunctionTypeEnum.Max,
                FUNCTION_TYPE_MIN => FunctionTypeEnum.Min,
                FUNCTION_TYPE_SUM => FunctionTypeEnum.Sum,
                FUNCTION_TYPE_AVERAGE => FunctionTypeEnum.Average,
                _ => FunctionTypeEnum.None,
            };
        }

        private static string OrderMethodToString(OrderMethodEnum value)
        {
            return value switch
            {
                OrderMethodEnum.Ascending => ORDER_METHOD_ASCENDING,
                OrderMethodEnum.Descending => ORDER_METHOD_DESCENDING,
                OrderMethodEnum.Shuffle => ORDER_METHOD_SHUFFLE,
                OrderMethodEnum.ShuffleStable => ORDER_METHOD_SHUFFLE_STABLE,
                _ => ORDER_METHOD_ASCENDING,
            };
        }

        private static OrderMethodEnum StringToOrderMethod(string value)
        {
            return value switch
            {
                ORDER_METHOD_ASCENDING => OrderMethodEnum.Ascending,
                ORDER_METHOD_DESCENDING => OrderMethodEnum.Descending,
                ORDER_METHOD_SHUFFLE => OrderMethodEnum.Shuffle,
                ORDER_METHOD_SHUFFLE_STABLE => OrderMethodEnum.ShuffleStable,
                _ => OrderMethodEnum.Ascending,
            };
        }
    }

    public enum OrderMethodEnum
    {
        Ascending,
        Descending,
        Shuffle,
        ShuffleStable,
    }

    public enum ViewTypeEnum
    {
        Large,
        Medium,
    }

    public enum FunctionTypeEnum
    {
        None,
        ItemCount,
        Max,
        Min,
        Sum,
        Average,
    }

    public class JsonModel
    {
        [JsonPropertyName("LastFilter")]
        public FilterModel? LastFilter { get; set; }

        [JsonPropertyName("Filters")]
        public List<FilterModel?>? Filters { get; set; }
    }

    public class FilterModel
    {
        [JsonPropertyName("CollapsedGroups")]
        public List<string?>? CollapsedGroups { get; set; }

        [JsonPropertyName("ComicOrderMethod")]
        public string? ComicOrderMethod { get; set; }

        [JsonPropertyName("Expression")]
        public string? Expression { get; set; }

        [JsonPropertyName("GroupBy")]
        public JsonNode? GroupBy { get; set; }

        [JsonPropertyName("GroupOrderMethod")]
        public string? GroupOrderMethod { get; set; }

        [JsonPropertyName("GroupSortingFunction")]
        public string? GroupSortingFunction { get; set; }

        [JsonPropertyName("GroupSortingProperty")]
        public JsonNode? GroupSortingProperty { get; set; }

        [JsonPropertyName("IncludeHiddenComics")]
        public bool? IncludeHiddenComics { get; set; }

        [JsonPropertyName("Modified")]
        public bool? Modified { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("SaveSortingAndGroupingSettings")]
        public bool? SaveSortingAndGroupingSettings { get; set; }

        [JsonPropertyName("SaveViewConfig")]
        public bool? SaveViewSettings { get; set; }

        [JsonPropertyName("SortBy")]
        public JsonNode? SortBy { get; set; }

        [JsonPropertyName("ViewType")]
        public string? ViewType { get; set; }
    }
}
