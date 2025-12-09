// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Components;
using ComicReader.Common.Actions.Utils;
using ComicReader.Common.Utils;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReader.Common.BaseUI;

internal abstract class BasePage : Page, ILifecycleOwner
{
    private readonly SimpleLifecycleManager _lifecycleManager = new();
    private readonly PageLifecycleEventHandler _externalLifecycleHandler;
    private ILifecycle.State _externalLifecycleState = ILifecycle.State.Resumed;

    private bool _hasNavigatedTo = false;
    private bool _hasNavigatedFrom = false;
    private bool _isLoaded = false;
    private NavigationBundle? _navigationBundle;

    protected int WindowId { get; private set; } = 0;
    public bool Started => _lifecycleManager.GetLifecycle().GetState() >= ILifecycle.State.Started;
    public bool Resumed => _lifecycleManager.GetLifecycle().GetState() >= ILifecycle.State.Resumed;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;
    public ActionHandler PageActionHandler { get; } = new();

    public BasePage()
    {
        _externalLifecycleHandler = state =>
        {
            _externalLifecycleState = state;
            UpdateLifecycleState();
        };
        _lifecycleManager.Initialize(GetType().Name, new LifecycleHandler(this));

        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;

        MemoryLeakTracker.TrackObject(this);
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycleManager.GetLifecycle();
    }

    protected sealed override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
    }

    protected sealed override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        switch (e.NavigationMode)
        {
            case NavigationMode.New:
            case NavigationMode.Back:
            case NavigationMode.Forward:
                _navigationBundle = (NavigationBundle)e.Parameter;
                _hasNavigatedTo = true;
                UpdateLifecycleState();
                break;
            case NavigationMode.Refresh:
                break;
        }
    }

    protected sealed override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        switch (e.NavigationMode)
        {
            case NavigationMode.New:
            case NavigationMode.Back:
            case NavigationMode.Forward:
                _hasNavigatedFrom = true;
                UpdateLifecycleState();
                break;
            case NavigationMode.Refresh:
                break;
        }
    }

    protected virtual void OnStart(PageBundle bundle)
    {
    }

    protected virtual void OnResume()
    {
    }

    protected virtual void OnPause()
    {
    }

    protected virtual void OnStop()
    {
    }

    protected T? GetAbility<T>() where T : class
    {
        return _navigationBundle!.Communicator.GetAbility<T>();
    }

    protected IEventBus GetEventBus()
    {
        return App.Instance.WindowManager.GetEventBus(WindowId);
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        if (!((Page)sender).IsLoaded)
        {
            return;
        }

        _isLoaded = true;
        UpdateLifecycleState();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if (((Page)sender).IsLoaded)
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

        ILifecycle.State finalState;
        if (_hasNavigatedFrom)
        {
            finalState = ILifecycle.State.Stopped;
        }
        else
        {
            finalState = _hasNavigatedTo ?
                (_isLoaded ? ILifecycle.State.Resumed : ILifecycle.State.Started) :
                ILifecycle.State.Initialized;
            finalState = (ILifecycle.State)Math.Min((int)_externalLifecycleState, (int)finalState);
        }

        _lifecycleManager.SetState(finalState);
    }

    private class LifecycleHandler(BasePage page) : SimpleLifecycleManager.ILifecycleHandler
    {
        public void PreStart()
        {
            NavigationBundle bundle = page._navigationBundle!;

            int windowId = StringUtils.ParseInt(bundle.Bundle.GetString(RouterConstants.ARG_WINDOW_ID));
            if (windowId < 0)
            {
                throw new ArgumentException("Invalid window ID in navigation parameters: " + windowId);
            }

            page.WindowId = windowId;

            // Register action handler components and providers
            page.PageActionHandler.RegisterComponent<IMainWindowComponent>(new MainWindowComponent(windowId));
            ActionHandlerUtility.RegisterCommonProviders(page.PageActionHandler);

            page.GetAbility<ILifecycleAwareAbility>()!.RegisterPageLifecycleHandler(page._externalLifecycleHandler);
        }

        public void PostStart()
        {
            NavigationBundle bundle = page._navigationBundle!;
            page.OnStart(bundle.Bundle);
        }

        public void PreResume()
        {
        }

        public void PostResume()
        {
            page.OnResume();
        }

        public void PrePause()
        {
        }

        public void PostPause()
        {
            page.OnPause();
        }

        public void PreStop()
        {
            page.GetAbility<ILifecycleAwareAbility>()!.UnregisterPageLifecycleHandler(page._externalLifecycleHandler);
        }

        public void PostStop()
        {
            page.OnStop();
        }
    }
}
