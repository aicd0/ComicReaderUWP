// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderFrame : BaseUserControl
{
    public delegate void ReadyStateChangeListener(ReaderFrame container, bool isReady, string reason);
    private event ReadyStateChangeListener? ReadyStateChanged;

    private ReaderFrameViewModel? ViewModel { get; set; }

    private bool? _isReady = null;
    private ReaderImageCompositor? _imageCompositor;

    public ReaderFrame()
    {
        InitializeComponent();
    }

    public void Bind(ReaderFrameViewModel? model)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        ViewModel = model;

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RebindViewModel("Rebind by container");
    }

    public void SetReadyStateChangeHandler(ReadyStateChangeListener? handler)
    {
        ReadyStateChanged = handler;
    }

    protected override void OnResume()
    {
        base.OnResume();

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    protected override void OnPause()
    {
        base.OnPause();

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void ImageHost_Loaded(object sender, RoutedEventArgs e)
    {
        _imageCompositor = new(ImageHost);
    }

    private void ImageHost_Unloaded(object sender, RoutedEventArgs e)
    {
        _imageCompositor?.Dispose();
        _imageCompositor = null;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ReaderFrameViewModel? vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        ReaderImageCompositor? compositor = _imageCompositor;
        if (compositor is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ReaderFrameViewModel):
                RebindViewModel("Rebind by property");
                break;
            case nameof(ReaderFrameViewModel.LeftImageVisible):
                compositor.PlaceholderMode = vm.IsDualPage;
                compositor.SetImage(0, vm.LeftImageVisible ? vm.LeftImageSource : null,
                    (float)vm.LeftImageWidth, (float)vm.LeftImageHeight);
                break;
            case nameof(ReaderFrameViewModel.RightImageVisible):
                compositor.PlaceholderMode = vm.IsDualPage;
                compositor.SetImage(1, vm.RightImageVisible ? vm.RightImageSource : null,
                    (float)vm.RightImageWidth, (float)vm.RightImageHeight);
                break;
            case nameof(ReaderFrameViewModel.Scale):
                compositor.Scale = (float)vm.Scale;
                break;
            default:
                break;
        }
    }

    private void Boundary_Loaded(object sender, RoutedEventArgs e)
    {
        DispatchReadyStateChangeEvent("FrameLoaded");
    }

    private void Boundary_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatchReadyStateChangeEvent($"SizeChanged (W={e.NewSize.Width},H={e.NewSize.Height})");
    }

    private void RebindViewModel(string reason)
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
