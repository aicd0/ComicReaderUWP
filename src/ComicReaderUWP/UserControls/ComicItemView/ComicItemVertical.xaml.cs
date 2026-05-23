// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.UserControls.ComicItemView;

internal sealed partial class ComicItemVertical : BaseUserControl, IComicItemView
{
    public ComicItemViewModel? Item { get; private set; }

    private readonly CancellationSession _loadImageToken = new();
    private bool _isLoaded = false;
    private bool _imageRequested = false;

    public ComicItemVertical()
    {
        InitializeComponent();
        Loaded += ComicItemVertical_LoadedOrUnloaded;
        Unloaded += ComicItemVertical_LoadedOrUnloaded;
    }

    public void SetComicModel(ComicItemViewModel? item)
    {
        if (item == Item)
        {
            return;
        }

        Item = item;
        if (item == null)
        {
            ClearImage();
        }
        else
        {
            Bindings.Update();
            ClearImage();
            RequestImageIfNeeded();
        }
    }

    private void ComicItemVertical_LoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == IsLoaded)
        {
            return;
        }

        _isLoaded = IsLoaded;
        if (_isLoaded)
        {
            RequestImageIfNeeded();
        }
        else
        {
            ClearImage();
        }
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

    private void ClearImage()
    {
        _loadImageToken.Next();
        _imageRequested = false;
        SetImageSource(null);
    }

    private void RequestImageIfNeeded()
    {
        if (_imageRequested || !_isLoaded)
        {
            return;
        }

        ComicItemViewModel? item = Item;
        if (item is null)
        {
            return;
        }

        double imageWidth = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"] - 40.0;
        double imageHeight = (double)Application.Current.Resources["ComicItemVerticalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>
        {
            new(new ComicCoverImageSource(item.Comic), new LoadImageCallback(this, item)) {
                Width = imageWidth,
                Height = imageHeight,
            }
        };
        new SimpleImageLoader.Transaction(_loadImageToken.Token, tokens).Commit();
        _imageRequested = true;
    }

    private void SetImageSource(ImageSource? imageSource)
    {
        ImageHolder1.Source = imageSource;
        ImageHolder2.Source = imageSource;
    }

    private class LoadImageCallback(ComicItemVertical viewHolder, ComicItemViewModel viewModel) : IImageResultHandler
    {
        private readonly WeakReference<ComicItemVertical> _viewHolderRef = new(viewHolder);
        private readonly ComicItemViewModel _viewModel = viewModel;

        public void OnSuccess(DecodedImageModel result)
        {
            if (!_viewHolderRef.TryGetTarget(out ComicItemVertical? view) || !view.IsLoaded)
            {
                return;
            }

            if (view.Item != _viewModel)
            {
                return;
            }

            view.SetImageSource(result.Source);
        }

        public void OnFailure()
        {
        }
    }
}
