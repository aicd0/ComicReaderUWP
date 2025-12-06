// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

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
    private const string TAG = nameof(BasePage);

    private PageCommunicator? _communicator = null;
    private readonly PageStopEventHandler _pageStopHandler;
    private readonly SimpleLifecycle _lifecycle = new();

    private bool _isStarted = false;
    private bool _isResumed = false;
    private bool _isLoaded = false;

    protected int WindowId { get; private set; } = 0;
    public bool Started => _isStarted;
    public bool Resumed => _isResumed;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;
    public ActionHandler PageActionHandler { get; } = new();

    public BasePage()
    {
        _pageStopHandler = delegate
        {
            TryPause();
            TryStop();
        };

        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycle;
    }

    protected sealed override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        switch (e.NavigationMode)
        {
            case NavigationMode.New:
            case NavigationMode.Back:
            case NavigationMode.Forward:
                TryStart(e.Parameter);
                TryResume();
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
                TryPause();
                TryStop();
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

    /// <summary>
    /// Retrieves an ability of the specified type from the communicator. Must be called on the UI thread.
    /// </summary>
    /// <remarks>This method delegates the retrieval of the ability to the underlying communicator.  Ensure
    /// that the communicator is properly initialized and supports the requested ability type.</remarks>
    /// <typeparam name="T">The type of the ability to retrieve. Must be a reference type.</typeparam>
    /// <returns>An instance of the specified ability type if available; otherwise, <see langword="null"/>.</returns>
    protected T? GetAbility<T>() where T : class
    {
        return _communicator?.GetAbility<T>();
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
        TryResume();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if (((Page)sender).IsLoaded)
        {
            return;
        }

        _isLoaded = false;
        TryPause();
    }

    private void TryStart(object p)
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        LogLifecycleEvent("Start");
        _lifecycle.SetState(ILifecycle.State.Started);

        if (p is not NavigationBundle bundle)
        {
            Logger.F(TAG, "Invalid navigation parameter type: " + (p?.GetType().FullName ?? "null"));
            return;
        }

        WindowId = StringUtils.ParseInt(bundle.Bundle.GetString(RouterConstants.ARG_WINDOW_ID));
        if (WindowId < 0)
        {
            Logger.F(TAG, "Invalid window ID in navigation parameters: " + WindowId);
            return;
        }

        _communicator = bundle.Communicator;

        // Register action handler components and providers
        PageActionHandler.RegisterComponent<IMainWindowComponent>(new MainWindowComponent(WindowId));
        ActionHandlerUtility.RegisterCommonProviders(PageActionHandler);

        GetAbility<ICommonPageAbility>()?.RegisterPageStopHandler(_pageStopHandler);
        DebugUtils.TrackError(() => OnStart(bundle.Bundle));
    }

    private void TryResume()
    {
        if (!_isStarted || !_isLoaded || _isResumed)
        {
            return;
        }

        _isResumed = true;
        LogLifecycleEvent("Resume");
        _lifecycle.SetState(ILifecycle.State.Resumed);
        DebugUtils.TrackError(OnResume);
    }

    private void TryPause()
    {
        if (!_isResumed)
        {
            return;
        }

        _isResumed = false;
        LogLifecycleEvent("Pause");
        _lifecycle.SetState(ILifecycle.State.Started);
        DebugUtils.TrackError(OnPause);
    }

    private void TryStop()
    {
        if (_isResumed || !_isStarted)
        {
            return;
        }

        _isStarted = false;
        _communicator?.GetAbility<ICommonPageAbility>()?.UnregisterPageStopHandler(_pageStopHandler);
        LogLifecycleEvent("Stop");
        _lifecycle.SetState(ILifecycle.State.Stopped);
        DebugUtils.TrackError(OnStop);
    }

    private void LogLifecycleEvent(string eventName)
    {
        Logger.I(LogTag.N("PageLifecycle", GetType().Name), eventName);
    }
}
