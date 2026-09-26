// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.UserControls.Misc;
using ComicReaderUWP.ViewModels;
using ComicReaderUWP.Views.Dialogs.EditFilter;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.Storage;

namespace ComicReaderUWP.Views.Pages.Home;

internal sealed partial class HomePage : BasePage
{
    private const string TAG = nameof(HomePage);

    private readonly HomePageViewModel ViewModel = new();

    private readonly SearchNavigationBar _searchNavigationBar;

    private ComicFilterModel.ViewTypeEnum? _viewType = null;
    private Storyboard? _headerTextBlockAnimation = null;
    private double _lastGridViewVerticalOffset = 0.0;

    public HomePage()
    {
        InitializeComponent();
        _searchNavigationBar = new();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ItemsView.Initialize(PageActionHandler);
        ViewTypeSelector.ViewTypeChanged += ViewModel.SelectViewType;

        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.NewTab);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Document });
        GetMainPageAbility().SetCustomNavigationBar(_searchNavigationBar);

        ViewModel.Initialize(PageActionHandler, bundle.GetString(RouterConstants.ARG_FILTER_JSON));

        ObserveData();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            ViewModel.Refresh(filters: true, library: true);
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            ViewModel.Refresh(library: true);
        });

        GlobalEvent.Instance.FilterUpdated.Observe(this, _ =>
        {
            ViewModel.Refresh(filters: true);
        });

        GetMainPageAbility().RegisterRefreshHandler(this, () =>
        {
            ComicModel.RescanLibrary("HomePage#RefreshPage");
        });

        ViewModel.UrlLiveData.ObserveSticky(this, url =>
        {
            GetMainPageAbility().SetUrl(url);
        });

        ViewModel.FilterLiveData.ObserveSticky(this, UpdateFilters);

        ViewModel.GroupingEnabledLiveData.ObserveSticky(this, delegate (bool grouped)
        {
            if (grouped)
            {
                ItemsView.SetGroupedItems(ViewModel.GroupedComicItems);
            }
            else
            {
                ItemsView.SetItems(ViewModel.UngroupedComicItems);
            }
        });

        ViewModel.ViewTypeLiveData.ObserveSticky(this, delegate (ComicFilterModel.ViewTypeEnum type)
        {
            ViewTypeSelector.SelectedViewType = type;

            if (_viewType == type)
            {
                return;
            }

            _viewType = type;
            ItemsView.ItemViewType = type;
        });

        _searchNavigationBar.SearchTextChange += ViewModel.SetSearchText;

        _searchNavigationBar.SearchTextSubmitted += text =>
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, text);
            GetMainPageAbility().OpenInCurrentTab(route);
        };
    }

    //
    // Grid View
    //

    private void ItemsView_ScrollOffsetChanged(double verticalOffset)
    {
        {
            Thickness p = HeaderAreaGrid.Padding;
            double newTop = Math.Max(20 - verticalOffset, 6);
            if (p.Top != newTop)
            {
                p.Top = newTop;
                HeaderAreaGrid.Padding = p;
            }
        }

        {
            double newOpacity = Math.Min(1.0, Math.Max(0, verticalOffset - 10) * 0.05);
            if (HeaderAreaBackgroundGrid.Opacity != newOpacity)
            {
                HeaderAreaBackgroundGrid.Opacity = newOpacity;
            }
        }

        if (verticalOffset > 20 && _lastGridViewVerticalOffset <= 20)
        {
            AnimateHeaderTextBlockOpacity(0.0);
        }
        else if (verticalOffset < 20 && _lastGridViewVerticalOffset >= 20)
        {
            AnimateHeaderTextBlockOpacity(1.0);
        }

        _lastGridViewVerticalOffset = verticalOffset;
    }

    private void ItemsView_GroupCollapseRequested(ComicGroupViewModel group)
    {
        ViewModel.CollapseOrExpandGroup(group);
    }

    //
    // Animation
    //

    private void AnimateHeaderTextBlockOpacity(double to)
    {
        double from = HeaderTextBlock.Opacity;
        if (_headerTextBlockAnimation != null)
        {
            _headerTextBlockAnimation.Stop();
            _headerTextBlockAnimation = null;
        }
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(Math.Abs(to - from) * 0.2)),
        };
        Storyboard.SetTarget(animation, HeaderTextBlock);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _headerTextBlockAnimation = storyboard;
    }

    //
    // Filters
    //

    private void UpdateFilters(HomePageViewModel.FilterModel model)
    {
        if (model == null)
        {
            return;
        }

        BindDropDownButton(SortAndGroupDropDownButton, SortAndGroupDropDownButtonText, model.SortAndGroupDropDown);
        BindDropDownButton(FilterPresetDropDownButton, FilterPresetDropDownButtonText, model.FilterPresetDropDown);
    }

    private void BindDropDownButton(DropDownButton button, TextBlock buttonText, HomePageViewModel.DropDownButtonModel model)
    {
        if (button == null)
        {
            return;
        }

        buttonText.Text = model.Name;

        FlyoutBase flyout = button.Flyout;
        if (flyout is not MenuFlyout)
        {
            flyout = new MenuFlyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
            };
            button.Flyout = flyout;
        }

        var menuFlyout = (MenuFlyout)flyout;

        menuFlyout.Items.Clear();
        foreach (BaseMenuFlyoutItemModel item in model.Items)
        {
            menuFlyout.Items.Add(item.CreateMenuFlyoutItem());
        }
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }

    //
    // Comic Item
    //

    private void EditFilterButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var dialog = new EditFilterDialog(await ViewModel.GetFilter());
            await dialog.ShowAsync(WindowId);
            if (dialog.HasMadeChanges)
            {
                ViewModel.Refresh(clearFilter: true);
            }
        });
    }

    private void AddNewFolder()
    {
        CoroutineUtils.Run(async () =>
        {
            StorageFolder? folder = await FilePickerUtils.PickFolder(WindowId);
            if (folder == null)
            {
                return;
            }

            AppSettingsModel.AddComicFolder(folder.Path);
            ComicModel.RescanLibrary("HomePage#AddNewFolder");
        });
    }

    private void AddFolderHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        AddNewFolder();
    }

    private void RefreshHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ComicModel.RescanLibrary("RefreshPage");
    }

    //
    // More actions
    //

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            if (sender is not FrameworkElement fe)
            {
                return;
            }

            IReadOnlyList<ComicModel> snapshot = ViewModel.GetComics();
            List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(
                PageActionHandler, snapshot, ViewModel.ExpandAllGroups, ViewModel.CollapseAllGroups);
            if (menuItems.Count == 0)
            {
                return;
            }

            MenuFlyout flyout = new()
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            };
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            flyout.ShowAt(fe);
        });
    }
}
