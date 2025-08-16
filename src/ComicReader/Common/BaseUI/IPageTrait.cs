// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.BaseUI;

internal interface IPageTrait
{
    Type GetPageType();

    bool HasNavigationBar();

    bool ImmersiveMode();

    bool SupportMultiInstance();
}
