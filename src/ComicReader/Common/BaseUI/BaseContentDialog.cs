// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common.Utils;
using ComicReader.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.BaseUI;

public partial class BaseContentDialog : ContentDialog, ILifecycleOwner
{
    private readonly SimpleLifecycle _lifecycle = new();

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

    public Task<ContentDialogResult> ShowAsync(int windowId)
    {
        return DialogUtils.EnqueueDialogAsync(windowId, this);
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
}
