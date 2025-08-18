// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.SDK.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.BaseUI;

public partial class BaseContentDialog : ContentDialog, ILifecycleOwner
{
    private readonly SimpleLifecycle _lifecycle = new();
    private static bool _dialogShowing = false;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;

    public BaseContentDialog()
    {
        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycle;
    }

    public Task<ContentDialogResult> ShowAsync(XamlRoot root)
    {
        // https://learn.microsoft.com/en-us/windows/apps/design/controls/dialogs-and-flyouts/dialogs
        // https://github.com/microsoft/microsoft-ui-xaml/issues/4167
        XamlRoot = root;
        return ShowAsync();
    }

    public Task<ContentDialogResult> ShowAsync(XamlRoot root, ContentDialogPlacement placement)
    {
        // https://learn.microsoft.com/en-us/windows/apps/design/controls/dialogs-and-flyouts/dialogs
        // https://github.com/microsoft/microsoft-ui-xaml/issues/4167
        XamlRoot = root;
        return ShowAsync(placement);
    }

    protected virtual void OnStart()
    {
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _lifecycle.SetState(ILifecycle.State.Resumed);
        OnStart();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            return;
        }

        _lifecycle.SetState(ILifecycle.State.Stopped);
    }

    private new async Task<ContentDialogResult> ShowAsync()
    {
        if (!MainThreadUtils.IsMainThread())
        {
            Logger.AssertNotReachHere("3D9574E9379F6890");
            return await Task.FromResult(ContentDialogResult.None);
        }
        if (_dialogShowing)
        {
            return await Task.FromResult(ContentDialogResult.None);
        }
        _dialogShowing = true;
        try
        {
            return await base.ShowAsync();
        }
        finally
        {
            _dialogShowing = false;
        }
    }

    private new async Task<ContentDialogResult> ShowAsync(ContentDialogPlacement placement)
    {
        if (!MainThreadUtils.IsMainThread())
        {
            Logger.AssertNotReachHere("75F11C516DFCB74D");
            return await Task.FromResult(ContentDialogResult.None);
        }
        if (_dialogShowing)
        {
            Logger.AssertNotReachHere("F1C02930A5B65CE4");
            return await Task.FromResult(ContentDialogResult.None);
        }
        _dialogShowing = true;
        try
        {
            return await base.ShowAsync(placement);
        }
        finally
        {
            _dialogShowing = false;
        }
    }
}
