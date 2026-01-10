// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.ViewModels;
using ComicReaderUWP.Views.Dialogs.EditFilter;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

using Windows.Storage;

namespace ComicReaderUWP.Views.Pages.Home;

internal sealed partial class HomePage : BasePage
{
    private const string TAG = nameof(HomePage);

    private readonly HomePageViewModel ViewModel = new();

    private ScrollViewer? _comicGridScrollViewer;

    private ComicFilterModel.ViewTypeEnum? _viewType = null;
    private bool? _usingGroupSource = null;
    private Storyboard? _headerTextBlockAnimation = null;
    private double _lastGridViewVerticalOffset = 0.0;

    public HomePage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        GetMainPageAbility().SetTitle(StringResourceProvider.Instance.NewTab);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Document });

        PageActionHandler.RegisterProvider(new CustomActionProvider(new CustomActionHandler(ViewModel)));

        ObserveData();
        ViewModel.Initialize(PageActionHandler, bundle.GetString(RouterConstants.ARG_FILTER_JSON));
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

        GetNavigationPageAbility().RegisterSearchTextChangeHandler(this, ViewModel.SetSearchText);

        GetNavigationPageAbility().RegisterRefreshHandler(this, () =>
        {
            ComicModel.UpdateAllComics("HomePage#RefreshPage");
        });

        ViewModel.UrlLiveData.ObserveSticky(this, url =>
        {
            GetMainPageAbility().SetUrl(url);
        });

        ViewModel.FilterLiveData.ObserveSticky(this, UpdateFilters);

        ViewModel.GroupingEnabledLiveData.ObserveSticky(this, delegate (bool grouped)
        {
            if (_usingGroupSource == grouped)
            {
                return;
            }

            _usingGroupSource = grouped;

            if (grouped)
            {
                ComicGridView.SetBinding(ItemsControl.ItemsSourceProperty, new Binding()
                {
                    Source = GroupedComicItemSource,
                    Mode = BindingMode.OneWay,
                });
            }
            else
            {
                ComicGridView.SetBinding(ItemsControl.ItemsSourceProperty, new Binding()
                {
                    Source = UngroupedComicItemSource,
                    Mode = BindingMode.OneWay,
                });
            }
        });

        ViewModel.ViewTypeLiveData.ObserveSticky(this, delegate (ComicFilterModel.ViewTypeEnum type)
        {
            if (_viewType == type)
            {
                return;
            }
            _viewType = type;
            switch (type)
            {
                case ComicFilterModel.ViewTypeEnum.Large:
                    ComicGridView.ItemTemplate = LargeComicItemTemplate;
                    ComicGridView.ItemContainerStyle = (Style)Resources["VerticalComicItemContainerStyle"];
                    ComicGridView.DesiredWidth = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"];
                    ComicGridView.ItemHeight = (double)Application.Current.Resources["ComicItemVerticalDesiredHeight"];
                    break;
                case ComicFilterModel.ViewTypeEnum.Medium:
                    ComicGridView.ItemTemplate = MediumComicItemTemplate;
                    ComicGridView.ItemContainerStyle = (Style)Resources["SearchResultItemContainerExpandedStyle"];
                    ComicGridView.DesiredWidth = (double)Application.Current.Resources["ComicItemHorizontalDesiredWidth"];
                    ComicGridView.ItemHeight = (double)Application.Current.Resources["ComicItemHorizontalDesiredHeight"];
                    break;
                default:
                    Logger.AssertNotReachHere("DBC3B0E205A8C333");
                    break;
            }
        });
    }

    //
    // Grid View
    //

    private void ComicGridView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = ComicGridView.ChildrenBreadthFirst().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            _comicGridScrollViewer = scrollViewer;
            scrollViewer.ViewChanged += ComicGridScrollViewer_ViewChanged;
        }
        else
        {
            Logger.AssertNotReachHere("90801E4FD070C67A");
        }
    }

    private void ComicGridView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = _comicGridScrollViewer;
        if (scrollViewer != null)
        {
            scrollViewer.ViewChanged -= ComicGridScrollViewer_ViewChanged;
        }
        _comicGridScrollViewer = null;
    }

    private void ComicGridScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv)
        {
            return;
        }

        double verticalOffset = sv.VerticalOffset;

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

    private void OnAdaptiveGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is not IComicItemView viewHolder || args.Item is not ComicItemViewModel item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            viewHolder.SetComicModel(null);
        }
        else
        {
            viewHolder.SetComicModel(item);
        }
    }

    private void CollapseExpandGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is ComicGroupViewModel group)
        {
            ViewModel.CollapseOrExpandGroup(group);
        }
    }

    private void ComicGridView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SetSelectionMode(false);
    }

    private void ComicGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        List<ComicItemViewModel> selectedItems = [];
        foreach (object? item in ComicGridView.SelectedItems)
        {
            if (item is ComicItemViewModel comicItem)
            {
                selectedItems.Add(comicItem);
            }
        }
        ViewModel.SetSelection(selectedItems);
    }

    //
    // Command Bar
    //

    private void CommandBarSelectAllClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as AppBarToggleButton;
        if (button == null)
        {
            return;
        }
        if (button.IsChecked == true)
        {
            ComicGridView.SelectAll();
        }
        else
        {
            ComicGridView.DeselectRange(new ItemIndexRange(0, (uint)ComicGridView.Items.Count));
        }
    }

    private void CommandBarFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Favorite);
    }

    private void CommandBarUnFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Unfavorite);
    }

    private void CommandBarHideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Hide);
    }

    private void CommandBarUnhideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.Unhide);
    }

    private void CommandBarMarkAsReadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsRead);
    }

    private void CommandBarMarkAsReadingClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsReading);
    }

    private void CommandBarMarkAsUnreadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToSelection(ComicOperationType.MarkAsUnread);
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

        BindDropDownButton(ViewTypeDropDownButton, ViewTypeDropDownButtonText, model.ViewTypeDropDown);
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

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    //
    // Comic Item
    //

    private void EditFilterButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
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
        CoroutineUtils.Start(async () =>
        {
            StorageFolder? folder = await FilePickerUtils.PickFolder(WindowId);
            if (folder == null)
            {
                return;
            }

            AppSettingsModel.Instance.AddComicFolder(folder.Path);
            ComicModel.UpdateAllComics("HomePage#AddNewFolder");
        });
    }

    private void AddFolderHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        AddNewFolder();
    }

    private void RefreshHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        ComicModel.UpdateAllComics("RefreshPage");
    }

    //
    // More actions
    //

    private async void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        ComicModel? randomComic = ViewModel.GetRandomComic();
        List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(
            PageActionHandler, randomComic, ViewModel.ExpandAllGroups, ViewModel.CollapseAllGroups);
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
    }

    //
    // Types
    //

    private class CustomActionHandler(HomePageViewModel viewModel) : CustomActionProvider.IHandler
    {
        public void Handle(string source, string name, IReadOnlyList<string> args)
        {
            bool handled = true;
            switch (source)
            {
                case MenuFlyoutItemsCreator.CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU:
                    switch (name)
                    {
                        case MenuFlyoutItemsCreator.CUSTOM_ACTION_NAME_SELECT:
                            viewModel.SetSelectionMode(!viewModel.IsSelectMode);
                            break;
                        default:
                            handled = false;
                            break;
                    }
                    break;
                default:
                    handled = false;
                    break;
            }

            if (!handled)
            {
                Logger.F(TAG, $"Unknown action '{source}.{name}'");
            }
        }
    }
}
