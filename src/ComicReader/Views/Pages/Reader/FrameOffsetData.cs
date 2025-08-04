// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Views.Pages.Reader;

internal class FrameOffsetData
{
    public double ParallelBegin;
    public double ParallelCenter;
    public double ParallelEnd;
    public double PerpendicularCenter;

    public override string ToString()
    {
        return
            "XB=" + ParallelBegin.ToString() +
            ",XC=" + ParallelCenter.ToString() +
            ",XE=" + ParallelEnd.ToString() +
            ",YC=" + PerpendicularCenter.ToString();
    }
}
