// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReader.Common.Localization;
using ComicReader.Common.Misc;
using ComicReader.SDK.Database;

namespace ComicReader.Data.Models.Misc;

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

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public ExternalModel? GetModel()
    {
        return Read(ExternalModel.From);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(m =>
        {
            model.To(m);
            return true;
        });
        Save();
        GlobalEvent.Instance.FilterUpdated.Emit(0);
    }

    public class JsonModel
    {
        [JsonPropertyName("LastFilter")]
        public FilterModel? LastFilter { get; set; }

        [JsonPropertyName("LastFilterModified")]
        public bool? LastFilterModified { get; set; }

        [JsonPropertyName("Filters")]
        public List<FilterModel?>? Filters { get; set; } = new();
    }

    public class FilterModel
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("SortBy")]
        public JsonNode? SortBy { get; set; }

        [JsonPropertyName("ComicOrderMethod")]
        public string? ComicOrderMethod { get; set; }

        // Deprecated, use ComicOrderMethod instead
        [JsonPropertyName("SortByAscending")]
        public bool? SortByAscending { get; set; }

        [JsonPropertyName("GroupBy")]
        public JsonNode? GroupBy { get; set; }

        [JsonPropertyName("GroupOrderMethod")]
        public string? GroupOrderMethod { get; set; }

        // Deprecated, use GroupOrderMethod instead
        [JsonPropertyName("GroupByAscending")]
        public bool? GroupByAscending { get; set; }

        [JsonPropertyName("GroupSortingFunction")]
        public string? GroupSortingFunction { get; set; }

        [JsonPropertyName("GroupSortingProperty")]
        public JsonNode? GroupSortingProperty { get; set; }

        [JsonPropertyName("ViewType")]
        public string? ViewType { get; set; }

        [JsonPropertyName("SaveViewConfig")]
        public bool? SaveViewConfig { get; set; }

        [JsonPropertyName("Expression")]
        public string? Expression { get; set; }
    }

    public class ExternalModel
    {
        public ExternalFilterModel? LastFilter { get; set; }
        public bool LastFilterModified { get; set; }
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
                    var externalFilter = ExternalFilterModel.From(filter);
                    if (externalFilter != null)
                    {
                        filters.Add(externalFilter);
                    }
                }
            }

            return new ExternalModel
            {
                LastFilter = ExternalFilterModel.From(model.LastFilter),
                LastFilterModified = model.LastFilterModified ?? false,
                Filters = filters
            };
        }

        public void To(JsonModel model)
        {
            model.LastFilter = LastFilter?.To();
            model.LastFilterModified = LastFilterModified;
            model.Filters = Filters?.ConvertAll(x => x?.To()) ?? [];
        }
    }

    public class ExternalFilterModel
    {
        public string Name { get; set; } = "";
        public ComicPropertyModel SortBy { get; set; } = new();
        public OrderMethodEnum ComicOrderMethod { get; set; }
        public ComicPropertyModel? GroupBy { get; set; }
        public OrderMethodEnum GroupOrderMethod { get; set; }
        public FunctionTypeEnum GroupSortingFunction { get; set; } = FunctionTypeEnum.None;
        public ComicPropertyModel? GroupSortingProperty { get; set; }
        public ViewTypeEnum ViewType { get; set; }
        public bool SaveViewConfig { get; set; }
        public string Expression { get; set; } = "";

        public ExternalFilterModel Clone()
        {
            return From(To())!;
        }

        public FilterModel To()
        {
            return new FilterModel
            {
                Name = Name,
                SortBy = SortBy.ToJson(),
                ComicOrderMethod = OrderMethodToString(ComicOrderMethod),
                GroupBy = GroupBy?.ToJson(),
                GroupOrderMethod = OrderMethodToString(GroupOrderMethod),
                GroupSortingFunction = FunctionTypeToString(GroupSortingFunction),
                GroupSortingProperty = GroupSortingProperty?.ToJson(),
                ViewType = ViewTypeToString(ViewType),
                SaveViewConfig = SaveViewConfig,
                Expression = Expression,
            };
        }

        public static ExternalFilterModel? From(FilterModel? model)
        {
            if (model == null)
            {
                return null;
            }

            return new ExternalFilterModel
            {
                Name = model.Name ?? "",
                SortBy = ComicPropertyModel.FromJson(model.SortBy) ?? new(),
                ComicOrderMethod = string.IsNullOrEmpty(model.ComicOrderMethod) ?
                    (model.SortByAscending ?? false ? OrderMethodEnum.Ascending : OrderMethodEnum.Descending) :
                    StringToOrderMethod(model.ComicOrderMethod),
                GroupBy = ComicPropertyModel.FromJson(model.GroupBy),
                GroupOrderMethod = string.IsNullOrEmpty(model.GroupOrderMethod) ?
                    (model.GroupByAscending ?? false ? OrderMethodEnum.Ascending : OrderMethodEnum.Descending) :
                    StringToOrderMethod(model.GroupOrderMethod),
                GroupSortingFunction = StringToFunctionType(model.GroupSortingFunction ?? FUNCTION_TYPE_NONE),
                GroupSortingProperty = ComicPropertyModel.FromJson(model.GroupSortingProperty),
                ViewType = StringToViewType(model.ViewType ?? ""),
                SaveViewConfig = model.SaveViewConfig ?? true,
                Expression = model.Expression ?? "",
            };
        }

        public static ExternalFilterModel FromDefault()
        {
            return new ExternalFilterModel
            {
                Name = StringResourceProvider.Instance.Default,
                ViewType = ViewTypeEnum.Large,
                SaveViewConfig = false,
                SortBy = new(),
                ComicOrderMethod = OrderMethodEnum.Ascending,
                GroupBy = null,
                GroupOrderMethod = OrderMethodEnum.Ascending,
                Expression = string.Empty,
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
            if (string.IsNullOrEmpty(value))
            {
                return ViewTypeEnum.Large;
            }
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
}
