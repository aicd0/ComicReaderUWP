// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Reader;

internal sealed partial class ReaderFrame : UserControl
{
    private bool _isLoaded = false;
    private bool? _isReady = null;

    public delegate void ReadyStateChangeListener(FrameworkElement container, bool isReady, string reason);
    private event ReadyStateChangeListener? ReadyStateChanged;

    public delegate void ImageChangeListener(ReaderFrameViewModel model);
    private event ImageChangeListener? ImageChanged;

    private ReaderFrameViewModel? ViewModel { get; set; }
    private FrameworkElement Container => MainFrame;

    public ReaderFrame()
    {
        InitializeComponent();

        Loaded += OnLoadedOrUnloaded;
        Unloaded += OnLoadedOrUnloaded;
    }

    private void OnLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        if (IsLoaded == _isLoaded)
        {
            return;
        }

        _isLoaded = IsLoaded;

        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (_isLoaded)
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }
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

    public void SetImageChangeHandler(ImageChangeListener? handler)
    {
        ImageChanged = handler;
    }

    private void OnFrameLoaded(object sender, RoutedEventArgs e)
    {
        DispatchReadyStateChangeEvent("FrameLoaded");
    }

    private void OnFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatchReadyStateChangeEvent($"SizeChanged (W={e.NewSize.Width},H={e.NewSize.Height})");
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReaderFrameViewModel))
        {
            RebindViewModel("Rebind by property");
        }
        else if (e.PropertyName == nameof(ReaderFrameViewModel.ImageLeft) || e.PropertyName == nameof(ReaderFrameViewModel.ImageRight))
        {
            if (ViewModel != null)
            {
                ImageChanged?.Invoke(ViewModel);
            }
        }
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
            ReadyStateChanged?.Invoke(Container, isReady, reason);
        }
    }

    private bool IsReady()
    {
        FrameworkElement container = Container;
        ReaderFrameViewModel? model = ViewModel;
        if (container == null || model == null)
        {
            return false;
        }

        double desired_width = model.FrameWidth + model.FrameMargin.Left + model.FrameMargin.Right;
        double desired_height = model.FrameHeight + model.FrameMargin.Top + model.FrameMargin.Bottom;

        if (Math.Abs(container.ActualWidth - desired_width) > 5.0)
        {
            return false;
        }

        if (Math.Abs(container.ActualHeight - desired_height) > 5.0)
        {
            return false;
        }

        return true;
    }
}
