// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderFrame : BaseUserControl
{
    public delegate void ReadyStateChangeListener(ReaderFrame container, bool isReady, string reason);
    private event ReadyStateChangeListener? ReadyStateChanged;

    private ReaderFrameViewModel? ViewModel { get; set; }

    private bool? _isReady = null;
    private ReaderImageCompositor? _imageCompositor;

    private readonly IValueObserver<bool> _rebindObserver;
    private readonly IValueObserver<bool> _leftImageVisibleObserver;
    private readonly IValueObserver<bool> _rightImageVisibleObserver;
    private readonly IValueObserver<double> _scaleObserver;

    public ReaderFrame()
    {
        InitializeComponent();

        _rebindObserver = ObserverUtils.Create<bool>(_ =>
        {
            UpdateBindings("RebindByUpdate");
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

    public void SetReadyStateChangeHandler(ReadyStateChangeListener? handler)
    {
        ReadyStateChanged = handler;
    }

    public void SetViewModel(ReaderFrameViewModel? model)
    {
        DisconnectViewModel();
        ViewModel = model;
        if (IsResumed)
        {
            ConnectViewModel();
        }

        UpdateBindings("RebindByContainer");
    }

    protected override void OnResume()
    {
        base.OnResume();
        _imageCompositor = new(ImageHost);
        ConnectViewModel();
    }

    protected override void OnPause()
    {
        base.OnPause();
        DisconnectViewModel();
        _imageCompositor?.Dispose();
        _imageCompositor = null;
    }

    private void Boundary_Loaded(object sender, RoutedEventArgs e)
    {
        DispatchReadyStateChangeEvent("FrameLoaded");
    }

    private void Boundary_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatchReadyStateChangeEvent($"SizeChanged (W={e.NewSize.Width},H={e.NewSize.Height})");
    }

    private void ConnectViewModel()
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        vm.RebindLiveData.ObserveSticky(this, _rebindObserver);
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
        vm.LeftImageVisibleLiveData.RemoveObserver(_leftImageVisibleObserver);
        vm.RightImageVisibleLiveData.RemoveObserver(_rightImageVisibleObserver);
        vm.ScaleLiveData.RemoveObserver(_scaleObserver);
    }

    private void UpdateBindings(string reason)
    {
        Bindings.Update();
        _isReady = null;
        DispatchReadyStateChangeEvent(reason);
    }

    private void DispatchReadyStateChangeEvent(string reason)
    {
        bool isReady = IsReady();
        if (isReady != _isReady)
        {
            _isReady = isReady;
            ReadyStateChanged?.Invoke(this, isReady, reason);
        }
    }

    private bool IsReady()
    {
        ReaderFrameViewModel? model = ViewModel;
        if (model is null)
        {
            return false;
        }

        double desiredWidth = model.FrameWidth + model.FrameMargin.Left + model.FrameMargin.Right;
        double desiredHeight = model.FrameHeight + model.FrameMargin.Top + model.FrameMargin.Bottom;

        if (Math.Abs(ActualWidth - desiredWidth) > 5.0)
        {
            return false;
        }

        if (Math.Abs(ActualHeight - desiredHeight) > 5.0)
        {
            return false;
        }

        return true;
    }
}
