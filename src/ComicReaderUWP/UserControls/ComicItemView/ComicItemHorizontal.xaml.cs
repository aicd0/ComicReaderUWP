// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.ComicItemView;

internal sealed partial class ComicItemHorizontal : BaseUserControl, IComicItemView
{
    private readonly CancellationSession _loadImageToken = new();

    public ComicItemViewModel? Item { get; private set; }

    public ComicItemHorizontal()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsLoaded)
        {
            return;
        }

        ComicItemViewModel? item = Item;
        if (item != null)
        {
            RequestImageIfNeeded(item);
        }
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.IsLoaded)
        {
            return;
        }

        _loadImageToken.Next();
        ComicItemViewModel? item = Item;
        if (item != null)
        {
            item.Image.ImageRequested = false;
        }
    }

    private void UserControl_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent tap events being dispatched to other controls
        e.Handled = true;
    }

    private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Item?.OnClick?.Invoke();
    }

    private async void RootGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
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

        args.Handled = true;
    }

    public void Bind(ComicItemViewModel item)
    {
        if (item != Item)
        {
            item.Image.ImageRequested = false;
            Item = item;
            Bindings.Update();
        }

        BindImage(item);
        RequestImageIfNeeded(item);
    }

    public void Unbind()
    {
        ComicItemViewModel? item = Item;
        Item = null;
        if (item != null)
        {
            item.Image.ImageRequested = false;
            item.Image.Image = null;
        }
    }

    private void BindImage(ComicItemViewModel item)
    {
        ImageHolder.Source = item.Image.Image;
    }

    private void RequestImageIfNeeded(ComicItemViewModel item)
    {
        if (item.Image.Image != null || item.Image.ImageRequested)
        {
            return;
        }
        item.Image.ImageRequested = true;
        double imageWidth = (double)Application.Current.Resources["ComicItemHorizontalImageWidth"];
        double imageHeight = (double)Application.Current.Resources["ComicItemHorizontalImageHeight"];
        var tokens = new List<SimpleImageLoader.Token>
        {
            new(new ComicCoverImageSource(item.Comic), new LoadImageCallback(this, item)) {
                Width = imageWidth,
                Height = imageHeight,
                Multiplication = 1.4,
                StretchMode = StretchModeEnum.UniformToFill,
            }
        };
        new SimpleImageLoader.Transaction(_loadImageToken.Token, tokens).Commit();
    }

    private class LoadImageCallback : IImageResultHandler
    {
        private readonly ComicItemHorizontal _viewHolder;
        private readonly ComicItemViewModel _viewModel;

        public LoadImageCallback(ComicItemHorizontal viewHolder, ComicItemViewModel viewModel)
        {
            _viewHolder = viewHolder;
            _viewModel = viewModel;
        }

        public void OnSuccess(DecodedImageModel result)
        {
            _viewModel.Image.Image = ImagingUtils.CreateImageSource(result);
            if (_viewModel == _viewHolder.Item)
            {
                _viewHolder.BindImage(_viewModel);
            }
        }

        public void OnFailure()
        {
        }
    }
}
