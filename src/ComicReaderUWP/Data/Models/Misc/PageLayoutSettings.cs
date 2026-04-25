// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ComicReaderUWP.Data.Models.Misc;

internal class PageLayoutSettings
{
    public bool TwoPageMode { get; set; } = false;
    public bool EnableCover { get; set; } = true;
    public bool SwapLeftAndRightPages { get; set; } = false;
    public bool SpreadDetection { get; set; } = true;

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

        return TwoPageMode == other.TwoPageMode &&
            EnableCover == other.EnableCover &&
            SwapLeftAndRightPages == other.SwapLeftAndRightPages &&
            SpreadDetection == other.SpreadDetection;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TwoPageMode);
        hash.Add(EnableCover);
        hash.Add(SwapLeftAndRightPages);
        hash.Add(SpreadDetection);
        return hash.ToHashCode();
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
            EnableCover = EnableCover,
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

        return new()
        {
            TwoPageMode = jsonModel.TwoPageMode ?? defaultModel.TwoPageMode,
            EnableCover = jsonModel.EnableCover ?? defaultModel.EnableCover,
            SwapLeftAndRightPages = jsonModel.SwapLeftAndRightPages ?? defaultModel.SwapLeftAndRightPages,
            SpreadDetection = jsonModel.SpreadDetection ?? defaultModel.SpreadDetection,
        };
    }

    public class JsonModel
    {
        [JsonPropertyName("TwoPageMode")]
        public bool? TwoPageMode { get; set; }

        [JsonPropertyName("EnableCover")]
        public bool? EnableCover { get; set; }

        [JsonPropertyName("SwapLeftAndRightPages")]
        public bool? SwapLeftAndRightPages { get; set; }

        [JsonPropertyName("SpreadDetection")]
        public bool? SpreadDetection { get; set; }
    }
}
