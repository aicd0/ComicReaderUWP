// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ComicReaderUWP.Data.Models.Misc;

internal class PageLayoutSettings
{
    public bool TwoPageMode { get; set; } = false;
    public int CoverPageCount { get; set; } = 1;
    public bool SwapLeftAndRightPages { get; set; } = false;
    public bool SpreadDetection { get; set; } = false;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not PageLayoutSettings other)
        {
            return false;
        }

        return
            TwoPageMode == other.TwoPageMode &&
            CoverPageCount == other.CoverPageCount &&
            SwapLeftAndRightPages == other.SwapLeftAndRightPages &&
            SpreadDetection == other.SpreadDetection;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TwoPageMode);
        hash.Add(CoverPageCount);
        hash.Add(SwapLeftAndRightPages);
        hash.Add(SpreadDetection);
        return hash.ToHashCode();
    }

    public PageLayoutSettings Clone()
    {
        return (PageLayoutSettings)MemberwiseClone();
    }

    public static bool operator ==(PageLayoutSettings? left, PageLayoutSettings? right)
    {
        return EqualityComparer<PageLayoutSettings>.Default.Equals(left, right);
    }

    public static bool operator !=(PageLayoutSettings? left, PageLayoutSettings? right)
    {
        return !(left == right);
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

    public static PageLayoutSettings FromJsonModel(JsonModel? jsonModel)
    {
        var defaultModel = new PageLayoutSettings();

        if (jsonModel is null)
        {
            return defaultModel;
        }

        int? legacyCoverPageCount = jsonModel.EnableCover.HasValue ? (jsonModel.EnableCover.Value ? 1 : 0) : null;
        return new()
        {
            TwoPageMode = jsonModel.TwoPageMode ?? defaultModel.TwoPageMode,
            CoverPageCount = jsonModel.CoverPageCount ?? legacyCoverPageCount ?? defaultModel.CoverPageCount,
            SwapLeftAndRightPages = jsonModel.SwapLeftAndRightPages ?? defaultModel.SwapLeftAndRightPages,
            SpreadDetection = jsonModel.SpreadDetection ?? defaultModel.SpreadDetection,
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
