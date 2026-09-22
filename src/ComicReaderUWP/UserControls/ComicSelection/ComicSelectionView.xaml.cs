// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.ComicSelection;

internal sealed partial class ComicSelectionView : BaseUserControl
{
    private const string TAG = nameof(ComicSelectionView);

    public delegate void GroupCollapseRequestedEventHandler(ComicGroupViewModel group);
    public event GroupCollapseRequestedEventHandler? GroupCollapseRequested;

    public delegate void ScrollOffsetChangedEventHandler(double verticalOffset);
    public event ScrollOffsetChangedEventHandler? ScrollOffsetChanged;

    public ComicSelectionViewModel ViewModel => (ComicSelectionViewModel)DataContext;

    public ComicFilterModel.ViewTypeEnum ItemViewType
    {
        get => (ComicFilterModel.ViewTypeEnum)GetValue(ItemViewTypeProperty);
        set => SetValue(ItemViewTypeProperty, value);
    }

    public static readonly DependencyProperty ItemViewTypeProperty = DependencyProperty.Register(
        nameof(ItemViewType),
        typeof(ComicFilterModel.ViewTypeEnum),
        typeof(ComicSelectionView),
        new PropertyMetadata(ComicFilterModel.ViewTypeEnum.Medium, OnItemViewTypeChanged));

    private static void OnItemViewTypeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((ComicSelectionView)sender).ApplyItemViewType();
    }

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public static readonly DependencyProperty ContentPaddingProperty = DependencyProperty.Register(
        nameof(ContentPadding),
        typeof(Thickness),
        typeof(ComicSelectionView),
        new PropertyMetadata(new Thickness(0), OnContentPaddingChanged));

    private static void OnContentPaddingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((ComicSelectionView)sender).ApplyContentPadding();
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent),
        typeof(object),
        typeof(ComicSelectionView),
        new PropertyMetadata(null));

    private readonly ObservableCollection<ComicItemViewModel> _items = [];
    private readonly ObservableCollection<ComicGroupViewModel> _groups = [];
    private readonly ICollectionView _groupedView;
    private ScrollViewer? _itemsScrollViewer;
    private bool? _usingGroupedSource = null;
    private ActionHandler _actionHandler = ActionHandler.Dummy;

    public ComicSelectionView()
    {
        InitializeComponent();

        DataContextChanged += (sender, args) => Bindings.Update();

        GroupedItemSource.Source = _groups;
        _groupedView = GroupedItemSource.View;

        ApplyContentPadding();
        ApplyItemViewType();
    }

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        _actionHandler.RegisterProvider(new CustomActionProvider(new SelectActionHandler(this)));
    }

    public void SetItems(IEnumerable<ComicItemViewModel> items)
    {
        List<ComicItemViewModel> newItems = [.. items];
        DiffUtils.UpdateCollection(_items, newItems, (x, y) => x.Comic.Id == y.Comic.Id, (x, y) => x.Update(y));
        ApplyItemsSource(false);
        ViewModel.UpdateCommandBarButtonStates();
    }

    public void SetGroupedItems(IEnumerable<ComicGroupViewModel> groups)
    {
        List<ComicGroupViewModel> newGroups = [.. groups];

        // Disable ME as it's causing a native crash in Microsoft.ui.xaml.dll. This is not guaranteed a fix
        // but so far it works fine. How to reproduce: Under group view (with 20+ groups), scroll to bottom
        // (or close to bottom) of the list. Then switch between different filter presets which share the
        // same group names, the crash should occur.
        DiffUtils.UpdateCollection(_groups, newGroups, (x, y) => x.GroupName == y.GroupName, (x, y) =>
        {
            x.Collapsed = y.Collapsed;
            x.Description = y.Description;
            x.UpdateItems(y.Items, (a, b) => a.Comic.Id == b.Comic.Id, (a, b) => a.Update(b));
        }, disableME: true);

        ApplyItemsSource(true);
        ViewModel.UpdateCommandBarButtonStates();
    }

    private void ApplyItemsSource(bool grouped)
    {
        if (_usingGroupedSource == grouped)
        {
            return;
        }

        _usingGroupedSource = grouped;
        ItemsGrid.ItemsSource = grouped ? _groupedView : _items;
    }

    private void ApplyItemViewType()
    {
        switch (ItemViewType)
        {
            case ComicFilterModel.ViewTypeEnum.Large:
                ItemsGrid.ItemTemplate = LargeItemTemplate;
                ItemsGrid.ItemContainerStyle = (Style)Resources["VerticalComicItemContainerStyle"];
                ItemsGrid.DesiredWidth = (double)Application.Current.Resources["ComicItemVerticalDesiredWidth"];
                ItemsGrid.ItemHeight = (double)Application.Current.Resources["ComicItemVerticalDesiredHeight"];
                break;
            case ComicFilterModel.ViewTypeEnum.Medium:
                ItemsGrid.ItemTemplate = MediumItemTemplate;
                ItemsGrid.ItemContainerStyle = (Style)Resources["SearchResultItemContainerExpandedStyle"];
                ItemsGrid.DesiredWidth = (double)Application.Current.Resources["ComicItemHorizontalDesiredWidth"];
                ItemsGrid.ItemHeight = (double)Application.Current.Resources["ComicItemHorizontalDesiredHeight"];
                break;
            default:
                Logger.AssertNotReachHere("8A1F72C69B4D5307");
                break;
        }
    }

    private void ApplyContentPadding()
    {
        Thickness padding = ContentPadding;
        padding.Left += 35.0;
        ItemsGrid.Padding = padding;
    }

    //
    // Command Bar
    //

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not AppBarToggleButton button)
        {
            return;
        }

        if (button.IsChecked == true)
        {
            ItemsGrid.SelectAll();
        }
        else
        {
            ItemsGrid.DeselectRange(new ItemIndexRange(0, (uint)ItemsGrid.Items.Count));
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        FavoriteModel.Instance.BatchAdd([.. comics.Select(x => new FavoriteModel.FavoriteItem
        {
            Id = x.Id,
            Title = x.Title,
        })]);
    }

    private void UnfavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        FavoriteModel.Instance.BatchRemoveWithId([.. comics.Select(x => x.Id)]);
    }

    private void CompletionStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor)
        {
            return;
        }

        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        List<BaseMenuFlyoutItemModel> menuItems = MenuFlyoutItemsCreator.CreateCompletionStatusMenuItems(comics);

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(anchor, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Top });
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedComicsHidden(true);
    }

    private void UnhideButton_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedComicsHidden(false);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        string idList = string.Join(',', comics.Select(x => x.Id.ToString()));
        ActionModel actionModel = ActionModel.Builder.Create(RemoveComicProvider.NAME)
            .AddParameter(RemoveComicProvider.PARAM_COMIC_ID, idList)
            .Build();
        _actionHandler.HandleNoResult(actionModel);
    }

    private void SetSelectedComicsHidden(bool hidden)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(() =>
        {
            return Task.WhenAll(comics.Select(x => x.SetHidden(hidden)));
        }));
    }

    private void ToggleSelectMode()
    {
        ViewModel.SetSelectMode(!ViewModel.IsSelectMode);
    }

    //
    // Item grid
    //

    private void ItemsGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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

    private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelection(ItemsGrid.SelectedItems.OfType<ComicItemViewModel>());
    }

    private void ItemsGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SetSelectMode(false);
    }

    private void ItemsGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = ItemsGrid.ChildrenBreadthFirst().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            _itemsScrollViewer = scrollViewer;
            scrollViewer.ViewChanged += ItemsScrollViewer_ViewChanged;
        }
        else
        {
            Logger.AssertNotReachHere("2E6B41D8795C0FA3");
        }
    }

    private void ItemsGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.IsLoaded)
        {
            return;
        }

        ScrollViewer? scrollViewer = _itemsScrollViewer;
        if (scrollViewer != null)
        {
            scrollViewer.ViewChanged -= ItemsScrollViewer_ViewChanged;
        }
        _itemsScrollViewer = null;
    }

    private void ItemsScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            ScrollOffsetChanged?.Invoke(scrollViewer.VerticalOffset);
        }
    }

    private void GroupCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ComicGroupViewModel group)
        {
            GroupCollapseRequested?.Invoke(group);
        }
    }

    //
    // Types
    //

    private class SelectActionHandler(ComicSelectionView view) : CustomActionProvider.IHandler
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
                            view.ToggleSelectMode();
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
