// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.ComicItemView;

internal sealed partial class ComicItemHorizontal : BaseUserControl, IComicItemView
{
    public ComicItemHorizontal()
    {
        InitializeComponent();
    }

    public ComicItemViewModel? Item { get; private set; }

    public void SetComicModel(ComicItemViewModel? item)
    {
        if (item == Item)
        {
            return;
        }

        Item = item;
        Bindings.Update();
    }

    private void UserControl_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent tap events being dispatched to other controls
        e.Handled = true;
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ComicItemViewModel? item = Item;
        if (item is null)
        {
            return;
        }

        item.OnClick?.Invoke(item);
    }

    private void RootGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        ComicItemViewModel? viewModel = Item;
        if (viewModel is null)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Run(async () =>
        {
            FlyoutBase? flyout = await viewModel.CreateContextFlyout();
            if (flyout is null)
            {
                return;
            }

            if (args.TryGetPosition(fe, out Windows.Foundation.Point point))
            {
                flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
            }
            else
            {
                flyout.ShowAt(fe);
            }
        });
    }
}
