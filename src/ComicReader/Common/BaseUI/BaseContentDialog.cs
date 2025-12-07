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
    private readonly SimpleLifecycleManager _lifecycleManager = new();

    private bool _isLoaded = false;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;

    public BaseContentDialog()
    {
        _lifecycleManager.Initialize(GetType().Name, new LifecycleHandler(this));

        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycleManager.GetLifecycle();
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

        _isLoaded = true;
        UpdateLifecycleState();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            return;
        }

        _isLoaded = false;
        UpdateLifecycleState();
    }

    private void UpdateLifecycleState()
    {
        if (GetLifecycle().GetState() == ILifecycle.State.Stopped)
        {
            return;
        }

        ILifecycle.State finalState = _isLoaded ? ILifecycle.State.Resumed : ILifecycle.State.Stopped;
        _lifecycleManager.SetState(finalState);
    }

    private class LifecycleHandler(BaseContentDialog dialog) : SimpleLifecycleManager.ILifecycleHandler
    {
        public void PreStart()
        {
        }

        public void PostStart()
        {
            dialog.OnStart();
        }

        public void PreResume()
        {
        }

        public void PostResume()
        {
        }

        public void PrePause()
        {
        }

        public void PostPause()
        {
        }

        public void PreStop()
        {
        }

        public void PostStop()
        {
        }
    }
}
