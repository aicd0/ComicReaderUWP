// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.UserControls.Reader.PageLayout;

internal interface IPageLayoutManager
{
    bool EquivalentTo(IPageLayoutManager other);

    void Reset(int pageCount);

    void AddPage(int page, int width, int height);

    bool TryGetPageLayout(int page, [NotNullWhen(true)] out PageLayoutInfo? layout);
}
