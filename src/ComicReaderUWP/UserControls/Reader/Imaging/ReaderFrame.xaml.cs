// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderFrame : BaseUserControl
{
    private ReaderFrameViewModel? ViewModel { get; set; }

    private ReaderImageCompositor? _imageCompositor;

    private readonly IValueObserver<bool> _rebindObserver;
    private readonly IValueObserver<bool> _redrawImageObserver;
    private readonly IValueObserver<bool> _leftImageVisibleObserver;
    private readonly IValueObserver<bool> _rightImageVisibleObserver;
    private readonly IValueObserver<double> _scaleObserver;

    public ReaderFrame()
    {
        InitializeComponent();

        _rebindObserver = ObserverUtils.Create<bool>(_ =>
        {
            Bindings.Update();
        });

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

        _leftImageVisibleObserver = ObserverUtils.Create<bool>(visible =>
        {
            ReaderFrameViewModel? vm = ViewModel;
            ReaderImageCompositor? compositor = _imageCompositor;
            if (vm is null || compositor is null)
            {
                return;
            }

            compositor.Name = vm.Page.ToString();
            compositor.PlaceholderMode = vm.IsDualPage;
            compositor.SetImage(0, visible ? vm.LeftImageSource : null,
                (float)vm.LeftImageWidth, (float)vm.LeftImageHeight);
        });

        _rightImageVisibleObserver = ObserverUtils.Create<bool>(visible =>
        {
            ReaderFrameViewModel? vm = ViewModel;
            ReaderImageCompositor? compositor = _imageCompositor;
            if (vm is null || compositor is null)
            {
                return;
            }

            compositor.Name = vm.Page.ToString();
            compositor.PlaceholderMode = vm.IsDualPage;
            compositor.SetImage(1, visible ? vm.RightImageSource : null,
                (float)vm.RightImageWidth, (float)vm.RightImageHeight);
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

    private void ConnectViewModel()
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.RebindLiveData.ObserveSticky(this, _rebindObserver);
        vm.RedrawImageLiveDate.Observe(this, _redrawImageObserver);
        vm.LeftImageVisibleLiveData.ObserveSticky(this, _leftImageVisibleObserver);
        vm.RightImageVisibleLiveData.ObserveSticky(this, _rightImageVisibleObserver);
        vm.ScaleLiveData.ObserveSticky(this, _scaleObserver);
    }

    private void DisconnectViewModel()
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.RebindLiveData.RemoveObserver(_rebindObserver);
        vm.RedrawImageLiveDate.RemoveObserver(_redrawImageObserver);
        vm.LeftImageVisibleLiveData.RemoveObserver(_leftImageVisibleObserver);
        vm.RightImageVisibleLiveData.RemoveObserver(_rightImageVisibleObserver);
        vm.ScaleLiveData.RemoveObserver(_scaleObserver);
    }
}
