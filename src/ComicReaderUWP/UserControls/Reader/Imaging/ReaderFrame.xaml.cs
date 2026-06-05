// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderFrame : UserControl
{
    public delegate void ReadyStateChangeListener(ReaderFrame container, bool isReady, string reason);
    private event ReadyStateChangeListener? ReadyStateChanged;

    private ReaderFrameViewModel? ViewModel { get; set; }

    private bool _isLoaded = false;
    private bool? _isReady = null;
    private readonly Compositor _compositor;
    private readonly ContainerVisual _root;

    public ReaderFrame()
    {
        InitializeComponent();

        _compositor = ElementCompositionPreview.GetElementVisual(ImageHost).Compositor;
        _root = _compositor.CreateContainerVisual();
        ElementCompositionPreview.SetElementChildVisual(ImageHost, _root);

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

    private void ReaderFrame_Loaded(object sender, RoutedEventArgs e)
    {
        DispatchReadyStateChangeEvent("FrameLoaded");
    }

    private void ReaderFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DispatchReadyStateChangeEvent($"SizeChanged (W={e.NewSize.Width},H={e.NewSize.Height})");
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReaderFrameViewModel))
        {
            RebindViewModel("Rebind by property");
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
