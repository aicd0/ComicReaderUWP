// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;

using ComicReader.Common;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.Views.Pages.Navigation;

internal partial class NavigationPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<Route> OpenInNewWindowLiveData = new();
    public readonly MutableLiveData<Route> OpenInNewTabLiveData = new();
    public readonly MutableLiveData<bool> FullscreenLiveData = new();

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

    private List<BaseMenuFlyoutItemViewModel> _moreButtonFlyoutItems = [];
    public List<BaseMenuFlyoutItemViewModel> MoreButtonFlyoutItems
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

            foreach (BaseMenuFlyoutItemViewModel item in MoreButtonFlyoutItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            return flyout;
        }
    }

    public void UpdateMoreMenuItems()
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.NewTab)
        {
            Glyph = "\uE8A5",
            OnClick = () =>
            {
                OpenInNewTabLiveData.Emit(Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME));
            },
        });

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.NewWindow)
        {
            Glyph = "\uE78B",
            OnClick = () =>
            {
                OpenInNewWindowLiveData.Emit(Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME));
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        if (_isFullscreen)
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.ExitFullscreen)
            {
                Glyph = "\uE73F",
                OnClick = () =>
                {
                    FullscreenLiveData.Emit(false);
                },
            });
        }
        else
        {
            items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.EnterFullscreen)
            {
                Glyph = "\uE740",
                OnClick = () =>
                {
                    FullscreenLiveData.Emit(true);
                },
            });
        }

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Settings)
        {
            Glyph = "\uE713",
            OnClick = () =>
            {
                OpenInNewTabLiveData.Emit(Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SETTING));
            },
        });

        if (DebugUtils.DeveloperMode)
        {
            items.Add(new MenuFlyoutItemViewModel("Dev tools")
            {
                Glyph = "\uEC7A",
                OnClick = () =>
                {
                    OpenInNewWindowLiveData.Emit(Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS));
                },
            });
        }

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Exit)
        {
            Glyph = "\uF78A",
            OnClick = () =>
            {
                App.Instance.WindowManager.LockWindowStatus();
                Application.Current.Exit();
            },
        });

        MoreButtonFlyoutItems = items;
    }
}
