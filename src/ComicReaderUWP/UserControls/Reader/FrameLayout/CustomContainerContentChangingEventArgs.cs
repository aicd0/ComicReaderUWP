// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

public sealed class CustomContainerContentChangingEventArgs(int itemIndex, object? item, UIElement itemContainer, bool inRecycleQueue)
{
    public object? Item { get; } = item;
    public UIElement ItemContainer { get; } = itemContainer;
    public int ItemIndex { get; } = itemIndex;
    public bool InRecycleQueue { get; } = inRecycleQueue;
}
