// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.UserControls.Reader.Models;

internal struct FrameOffsetData
{
    public double ParallelStart;
    public double ParallelEnd;
    public double PerpendicularCenter;

    public override readonly string ToString()
    {
        return $"AS={ParallelStart}, AE={ParallelEnd}, BC={PerpendicularCenter}";
    }
}
