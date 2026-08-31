// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

internal interface IReaderListViewItemViewModel
{
    public double Width { get; }

    public double Height { get; }

    public Thickness Margin { get; }
}
