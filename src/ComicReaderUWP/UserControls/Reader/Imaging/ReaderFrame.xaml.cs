// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderFrame : BaseUserControl
{
    private ReaderFrameViewModel? ViewModel { get; set; }

    private ReaderImageCompositor? _imageCompositor;

    private readonly IValueObserver<bool> _redrawImageObserver;
    private readonly IValueObserver<double> _scaleObserver;

    public ReaderFrame()
    {
        InitializeComponent();

        _redrawImageObserver = ObserverUtils.Create<bool>(_ =>
        {
            ReaderFrameViewModel? vm = ViewModel;
            ReaderImageCompositor? compositor = _imageCompositor;
            if (vm is null || compositor is null)
            {
                return;
            }

            compositor.Invalidate();
        });

        _scaleObserver = ObserverUtils.Create<double>(scale =>
        {
            ReaderImageCompositor? compositor = _imageCompositor;
            if (compositor is null)
            {
                return;
            }

            compositor.Scale = (float)scale;
        });
    }

    protected override void OnStart()
    {
        base.OnStart();
        _imageCompositor = new(ImageHost);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ConnectViewModel();
    }

    protected override void OnStop()
    {
        base.OnStop();
        DisconnectViewModel();
        _imageCompositor?.Dispose();
        _imageCompositor = null;
    }

    private void ReaderFrame_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ReferenceEquals(ViewModel, args.NewValue))
        {
            return;
        }

        DisconnectViewModel();
        ViewModel = null;

        if (args.NewValue is ReaderFrameViewModel model)
        {
            ViewModel = model;
            if (IsResumed)
            {
                ConnectViewModel();
            }
        }
    }

    private void ImageHost_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        if (!args.TryGetPosition(fe, out Windows.Foundation.Point point))
        {
            return;
        }

        ReaderImageCompositor? compositor = _imageCompositor;
        if (compositor is null)
        {
            return;
        }

        int imageIndex = compositor.HitTest(new(point.X, point.Y));
        if (imageIndex < 0)
        {
            return;
        }

        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Run(async () =>
        {
            IReadOnlyList<BaseMenuFlyoutItemModel> menuItems = await vm.RequestImageContextMenu(imageIndex);

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            if (flyout.Items.Count == 0)
            {
                flyout.Items.Add(new MenuFlyoutItem()
                {
                    Text = StringResourceProvider.Instance.None,
                    IsEnabled = false,
                });
            }

            flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
        });
    }

    private void ConnectViewModel()
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.RedrawImageLiveDate.Observe(this, _redrawImageObserver);
        vm.ScaleLiveData.ObserveSticky(this, _scaleObserver);

        ReaderImageCompositor? compositor = _imageCompositor;
        if (compositor is not null)
        {
            compositor.Name = vm.Page.ToString();
            compositor.PlaceholderMode = vm.IsDualPage;
            compositor.SetImage(0, vm.LeftImageSource, (float)vm.LeftImageWidth, (float)vm.LeftImageHeight);
            compositor.SetImage(1, vm.RightImageSource, (float)vm.RightImageWidth, (float)vm.RightImageHeight);
        }
    }

    private void DisconnectViewModel()
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.RedrawImageLiveDate.RemoveObserver(_redrawImageObserver);
        vm.ScaleLiveData.RemoveObserver(_scaleObserver);

        ReaderImageCompositor? compositor = _imageCompositor;
        if (compositor is not null)
        {
            compositor.SetImage(0, null, 0, 0);
            compositor.SetImage(1, null, 0, 0);
        }
    }
}
