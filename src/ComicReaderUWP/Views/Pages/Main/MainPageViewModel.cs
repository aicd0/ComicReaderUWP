// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.Views.Pages.Main;

internal partial class MainPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LogItemViewModel> LogItems { get; } = [];

    private bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        }
    }

    private bool _isFullscreen = false;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFullscreen)));
            UpdateMoreMenuItems();
        }
    }

    private bool _isLogVisible = false;
    public bool IsLogVisible
    {
        get => _isLogVisible;
        set
        {
            _isLogVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLogVisible)));
        }
    }

    private bool _canGoBack = false;
    public bool CanGoBack
    {
        get => _canGoBack;
        set
        {
            _canGoBack = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoBack)));
        }
    }

    private bool _canGoForward = false;
    public bool CanGoForward
    {
        get => _canGoForward;
        set
        {
            _canGoForward = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoForward)));
        }
    }

    private bool _refreshing = false;
    public bool Refreshing
    {
        get => _refreshing;
        set
        {
            _refreshing = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Refreshing)));
        }
    }

    private bool _isHomePage = false;
    public bool IsHomePage
    {
        get => _isHomePage;
        set
        {
            _isHomePage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHomePage)));
        }
    }

    private string _sidebarButtonText = string.Empty;
    public string SidebarButtonText
    {
        get => _sidebarButtonText;
        set
        {
            _sidebarButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SidebarButtonText)));
        }
    }

    private string _sidebarButtonGlyph = string.Empty;
    public string SidebarButtonGlyph
    {
        get => _sidebarButtonGlyph;
        set
        {
            _sidebarButtonGlyph = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SidebarButtonGlyph)));
        }
    }

    private List<BaseMenuFlyoutItemModel> _moreButtonFlyoutItems = [];
    public List<BaseMenuFlyoutItemModel> MoreButtonFlyoutItems
    {
        get => _moreButtonFlyoutItems;
        set
        {
            _moreButtonFlyoutItems = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoreButtonFlyout)));
        }
    }

    public FlyoutBase? MoreButtonFlyout
    {
        get
        {
            if (MoreButtonFlyoutItems.Count == 0)
            {
                return null;
            }

            var flyout = new MenuFlyout()
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            };

            foreach (BaseMenuFlyoutItemModel item in MoreButtonFlyoutItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            return flyout;
        }
    }

    private ActionHandler _actionHandler = ActionHandler.Dummy;

    public MainPageViewModel()
    {
        _logListener = new LogListener(this);
    }

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        ShowOrHideLogger(AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_LOG_VISIBLE, false));
        StartOrStopLogger(AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_LOG_STARTED, true));
    }

    public void OnStop()
    {
        Logger.RemoveListener(_logListener);
    }

    public void UpdateSidebarButton(bool opened)
    {
        if (opened)
        {
            SidebarButtonGlyph = "\uE89F";
            SidebarButtonText = StringResourceProvider.Instance.CloseSidebar;
        }
        else
        {
            SidebarButtonGlyph = "\uE8A0";
            SidebarButtonText = StringResourceProvider.Instance.OpenSidebar;
        }
    }

    public void UpdateMoreMenuItems()
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.NewTab,
            Glyph = "\uE8A5",
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                    .Build();
                _actionHandler.Handle(actionModel);
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.NewWindow,
            Glyph = "\uE78B",
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
                    .Build();
                _actionHandler.Handle(actionModel);
            },
        });

        items.Add(new SeparatorMenuFlyoutItemModel());

        if (_isFullscreen)
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.ExitFullscreen,
                Glyph = "\uE73F",
                Click = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(FullscreenServiceProvider.NAME)
                        .AddParameter(FullscreenServiceProvider.PARAM_ENTER, "0")
                        .Build();
                    _actionHandler.Handle(actionModel);
                },
            });
        }
        else
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.EnterFullscreen,
                Glyph = "\uE740",
                Click = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(FullscreenServiceProvider.NAME)
                        .AddParameter(FullscreenServiceProvider.PARAM_ENTER, "1")
                        .Build();
                    _actionHandler.Handle(actionModel);
                },
            });
        }

        {
            var uiContext = UIContext.Create(_actionHandler);
            if (uiContext is not null)
            {
                var pluginItems = PluginManager.Instance.GetActivePlugins()
                    .SelectMany(ctx => ctx.GetMainPageMoreMenuItems(uiContext))
                    .ToImmutableList();
                if (pluginItems.Count > 0)
                {
                    items.Add(new SeparatorMenuFlyoutItemModel());
                    items.AddRange(pluginItems);
                }
            }
        }

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Settings,
            Glyph = "\uE713",
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SETTINGS);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                    .Build();
                _actionHandler.Handle(actionModel);
            },
        });

        if (DebugUtils.DeveloperMode)
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = "Dev tools",
                Glyph = "\uEC7A",
                Click = () =>
                {
                    var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS);
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
                        .Build();
                    _actionHandler.Handle(actionModel);
                },
            });
        }

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.CheckForUpdates,
            Glyph = "\uE895",
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    var uri = new Uri(@"https://github.com/aicd0/ComicReaderUWP/releases");
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                });
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Exit,
            Click = () =>
            {
                ApplicationService.StartShuttingDown();
                Application.Current.Exit();
            },
        });

        MoreButtonFlyoutItems = items;
    }

    //
    // Logs
    //

    private readonly LogListener _logListener;
    private bool _logStarted = false;

    public void StartOrStopLogger()
    {
        StartOrStopLogger(!_logStarted);
    }

    public void ShowOrHideLogger()
    {
        ShowOrHideLogger(!_isLogVisible);
    }

    private void StartOrStopLogger(bool started)
    {
        if (started && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (started == _logStarted)
        {
            return;
        }

        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_LOG_STARTED, started);
        _logStarted = started;
        if (started)
        {
            Logger.AddListener(_logListener);
        }
    }

    public void ShowOrHideLogger(bool visible)
    {
        if (visible && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (_isLogVisible == visible)
        {
            return;
        }

        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_LOG_VISIBLE, visible);
        IsLogVisible = visible;
    }

    private void AppendLog(string message)
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            LogItemViewModel item = new()
            {
                Text = message,
            };

            LogItems.Insert(0, item);
            while (LogItems.Count > 100)
            {
                LogItems.RemoveAt(LogItems.Count - 1);
            }
        });
    }

    private class LogListener(MainPageViewModel viewModel) : Logger.ILogListener
    {
        public void OnLog(Logger.LogItem item)
        {
            if (!viewModel._logStarted)
            {
                return;
            }

            if (item.Level <= 4)
            {
                List<LogTag?> consoleWhitelist = DebugSwitchModel.Instance.ConsoleWhitelist;
                if (!consoleWhitelist.Any(t => t is null || t.ContainsAny(item.Tag)))
                {
                    return;
                }
            }

            viewModel.AppendLog(item.DisplayMessage);
        }
    }
}
