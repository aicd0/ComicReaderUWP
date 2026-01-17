// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Actions.Utils;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ComicReaderUWP.Common.BaseUI;

internal abstract class BasePage : Page, ILifecycleOwner
{
    private readonly SimpleLifecycleManager _lifecycleManager = new();
    private readonly PageLifecycleEventHandler _externalLifecycleHandler;
    private ILifecycle.State _externalLifecycleState = ILifecycle.State.Resumed;

    private bool _hasNavigatedTo = false;
    private bool _hasNavigatedFrom = false;
    private bool _isLoaded = false;
    private INavigationBundle? _navigationBundle;

    protected int WindowId { get; private set; } = 0;
    public bool IsStarted => _lifecycleManager.GetLifecycle().GetState() >= ILifecycle.State.Started;
    public bool IsResumed => _lifecycleManager.GetLifecycle().GetState() >= ILifecycle.State.Resumed;

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
                _navigationBundle = (INavigationBundle)e.Parameter;
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
            INavigationBundle bundle = page._navigationBundle!;

            // Retrieve window ID
            IMainWindowAbility? mainWindowAbility = page.GetAbility<IMainWindowAbility>() ?? throw new InvalidOperationException("IMainWindowAbility not found");
            int windowId = mainWindowAbility.WindowId;
            page.WindowId = windowId;
            page.PageActionHandler.RegisterComponent<IMainWindowComponent>(new MainWindowComponent(windowId));

            // Retrieve tab ID (if has)
            IMainPageAbilityForTab? mainPageAbility = page.GetAbility<IMainPageAbilityForTab>();
            if (mainPageAbility is not null)
            {
                page.PageActionHandler.RegisterComponent<IMainPageComponent>(new MainPageComponent(mainPageAbility.TabId));
            }

            ActionHandlerUtility.RegisterCommonProviders(page.PageActionHandler);
            page.GetAbility<ILifecycleAwareAbility>()!.RegisterPageLifecycleHandler(page._externalLifecycleHandler);
        }

        public void PostStart()
        {
            INavigationBundle bundle = page._navigationBundle!;
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

    private class MainWindowComponent(int windowId) : IMainWindowComponent
    {
        public int WindowId => windowId;
    }

    private class MainPageComponent(string tabId) : IMainPageComponent
    {
        public string TabId => tabId;
    }
}
