// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Views.Pages.Reader;

internal class ZoomCoefficient
{
    public double FitWidth;
    public double FitHeight;

    public double Min()
    {
        return Math.Min(FitWidth, FitHeight);
    }

    public double Max()
    {
        return Math.Max(FitWidth, FitHeight);
    }

    public override string ToString()
    {
        return $"{{FW={FitWidth},FH={FitHeight}}}";
    }
}
