// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Data.Models.Misc;

public enum PageArrangementEnum
{
    Single, // 1 2 3 4 5
    DualCover, // 1 23 45
    DualCoverMirror, // 1 32 54
    DualNoCover, // 12 34 5
    DualNoCoverMirror, // 21 43 5
}
