// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.BaseUI;

public partial class BaseUserControl : UserControl, ILifecycleOwner
{
    private readonly SimpleLifecycleManager _lifecycleManager = new();
    private bool _isLoaded = false;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;

    public BaseUserControl()
    {
        _lifecycleManager.Initialize(GetType().Name, new LifecycleHandler(this));

        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;

        MemoryLeakTracker.TrackObject(this);
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycleManager.GetLifecycle();
    }

    protected virtual void OnStart()
    {
    }

    protected virtual void OnResume()
    {
    }

    protected virtual void OnPause()
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

        ILifecycle.State finalState = _isLoaded ? ILifecycle.State.Resumed : ILifecycle.State.Started;
        _lifecycleManager.SetState(finalState);
    }

    private class LifecycleHandler(BaseUserControl control) : SimpleLifecycleManager.ILifecycleHandler
    {
        public void PreStart()
        {
        }

        public void PostStart()
        {
            control.OnStart();
        }

        public void PreResume()
        {
        }

        public void PostResume()
        {
            control.OnResume();
        }

        public void PrePause()
        {
        }

        public void PostPause()
        {
            control.OnPause();
        }

        public void PreStop()
        {
        }

        public void PostStop()
        {
        }
    }
}
