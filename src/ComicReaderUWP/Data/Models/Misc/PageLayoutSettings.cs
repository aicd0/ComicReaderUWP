// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ComicReaderUWP.Data.Models.Misc;

internal class PageLayoutSettings
{
    public bool TwoPageMode { get; set; } = false;
    public int CoverPageCount { get; set; } = 1;
    public bool SwapLeftAndRightPages { get; set; } = false;
    public bool SpreadDetection { get; set; } = false;

    public PageLayoutSettings Clone()
    {
        return (PageLayoutSettings)MemberwiseClone();
    }

    public JsonModel ToJsonModel()
    {
        return new()
        {
            TwoPageMode = TwoPageMode,
            CoverPageCount = CoverPageCount,
            SwapLeftAndRightPages = SwapLeftAndRightPages,
            SpreadDetection = SpreadDetection,
        };
    }

    public static PageLayoutSettings FromJsonModel(JsonModel? model, PageLayoutSettings? fallbackModel = null)
    {
        fallbackModel ??= new PageLayoutSettings();

        if (model is null)
        {
            return fallbackModel;
        }

        int? legacyCoverPageCount = model.EnableCover.HasValue ? (model.EnableCover.Value ? 1 : 0) : null;
        return new()
        {
            TwoPageMode = model.TwoPageMode ?? fallbackModel.TwoPageMode,
            CoverPageCount = model.CoverPageCount ?? legacyCoverPageCount ?? fallbackModel.CoverPageCount,
            SwapLeftAndRightPages = model.SwapLeftAndRightPages ?? fallbackModel.SwapLeftAndRightPages,
            SpreadDetection = model.SpreadDetection ?? fallbackModel.SpreadDetection,
        };
    }

    public class JsonModel
    {
        [JsonPropertyName("TwoPageMode")]
        public bool? TwoPageMode { get; set; }

        [JsonPropertyName("CoverPageCount")]
        public int? CoverPageCount { get; set; }

        [JsonPropertyName("SwapLeftAndRightPages")]
        public bool? SwapLeftAndRightPages { get; set; }

        [JsonPropertyName("SpreadDetection")]
        public bool? SpreadDetection { get; set; }

        // Legacy
        [JsonPropertyName("EnableCover")]
        public bool? EnableCover { get; set; }
    }
}
