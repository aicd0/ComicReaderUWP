// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReaderUWP.Common.BaseUI;

internal interface IPageTrait
{
    Type PageType { get; }

    bool IsImmersiveMode { get; }

    bool AllowMultiplePages { get; }
}
