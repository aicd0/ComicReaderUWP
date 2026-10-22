// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Services;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Main;

internal partial class MainPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

    private ActionHandler _actionHandler = ActionHandler.Dummy;

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void ExitApp()
    {
        ApplicationService.StartExiting();
        Application.Current.Exit();
    }

    public void OpenDevTools()
    {
        if (!DebugUtils.DeveloperMode)
        {
            return;
        }

        var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS);
        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
            .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
            .Build();
        _actionHandler.HandleNoResult(actionModel);
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

    public List<BaseMenuFlyoutItemModel> CreateMoreMenuItems()
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.NewTab,
            Icon = new FontIconSource() { Glyph = "\uE8A5" },
            ShortcutAction = KeyboardShortcutActions.AddNewTab,
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                    .Build();
                _actionHandler.HandleNoResult(actionModel);
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.NewWindow,
            Icon = new FontIconSource() { Glyph = "\uE78B" },
            ShortcutAction = KeyboardShortcutActions.AddNewWindow,
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, "-1")
                    .Build();
                _actionHandler.HandleNoResult(actionModel);
            },
        });

        items.Add(new SeparatorMenuFlyoutItemModel());

        if (_isFullscreen)
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.ExitFullscreen,
                Icon = new FontIconSource() { Glyph = "\uE73F" },
                ShortcutAction = KeyboardShortcutActions.ToggleFullscreen,
                Click = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(FullscreenServiceProvider.NAME)
                        .AddParameter(FullscreenServiceProvider.PARAM_ENTER, "0")
                        .Build();
                    _actionHandler.HandleNoResult(actionModel);
                },
            });
        }
        else
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.EnterFullscreen,
                Icon = new FontIconSource() { Glyph = "\uE740" },
                ShortcutAction = KeyboardShortcutActions.ToggleFullscreen,
                Click = () =>
                {
                    ActionModel actionModel = ActionModel.Builder.Create(FullscreenServiceProvider.NAME)
                        .AddParameter(FullscreenServiceProvider.PARAM_ENTER, "1")
                        .Build();
                    _actionHandler.HandleNoResult(actionModel);
                },
            });
        }

        {
            var windowContext = PluginWindowContext.From(_actionHandler);
            var pluginItems = PluginManager.Instance.GetActivePlugins()
                .SelectMany(ctx => ctx.GetMainPageMoreMenuItems(windowContext))
                .ToImmutableList();
            if (pluginItems.Count > 0)
            {
                items.Add(new SeparatorMenuFlyoutItemModel());
                items.AddRange(pluginItems);
            }
        }

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Settings,
            Icon = new FontIconSource() { Glyph = "\uE713" },
            Click = () =>
            {
                var route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SETTINGS);
                ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                    .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                    .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                    .Build();
                _actionHandler.HandleNoResult(actionModel);
            },
        });

        if (DebugUtils.DeveloperMode)
        {
            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = "Dev tools",
                Icon = new FontIconSource() { Glyph = "\uEC7A" },
                ShortcutAction = KeyboardShortcutActions.OpenDevTools,
                Click = OpenDevTools,
            });
        }

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.CheckForUpdates,
            Icon = new FontIconSource() { Glyph = "\uE895" },
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    if (EnvironmentProvider.IsPortable())
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(StaticStringResources.GITHUB_RELEASES_URL));
                    }
                    else
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(StaticStringResources.MS_STORE_DEEP_LINK));
                    }
                });
            }
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Exit,
            ShortcutAction = KeyboardShortcutActions.ExitApp,
            Click = ExitApp,
        });

        return items;
    }
}