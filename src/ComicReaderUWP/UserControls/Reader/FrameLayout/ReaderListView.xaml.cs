// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

internal sealed partial class ReaderListView : BaseUserControl
{
    private const string TAG = nameof(ReaderListView);

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(ReaderListView), new PropertyMetadata(null, OnItemTemplateChanged));

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView view)
        {
            return;
        }

        view.RefreshAllItems();
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IReadOnlyList<IReaderListViewItemViewModel>), typeof(ReaderListView), new PropertyMetadata(Array.Empty<IReaderListViewItemViewModel>(), OnItemsSourceChanged));

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView view)
        {
            return;
        }

        if (e.NewValue is not IReadOnlyList<IReaderListViewItemViewModel> list)
        {
            throw new InvalidOperationException("ItemsSource has to be IReadOnlyList<IReaderListViewItemViewModel>.");
        }

        view._layoutCache.Items = list;

        view.UnsubscribeItemsSourceChange();
        view._collectionChangedSource = null;

        if (e.NewValue is INotifyCollectionChanged notifyCollection)
        {
            view._collectionChangedSource = notifyCollection;
            view.SubscribeItemsSourceChange();
        }

        view.RefreshAllItems();
    }

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ReaderListView), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView view)
        {
            return;
        }

        view.RefreshAllItems();
    }

    private INotifyCollectionChanged? _collectionChangedSource;
    private readonly LayoutCache _layoutCache = new();
    private readonly List<int> _containerToItemIndexMapper = [];
    private readonly List<int> _visibleItemIndices = [];
    private readonly Queue<UIElement> _recycledContainers = [];
    private bool _isChanging = false;

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IReadOnlyList<IReaderListViewItemViewModel> ItemsSource
    {
        get => (IReadOnlyList<IReaderListViewItemViewModel>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public UIElement ItemsPanelRoot => ContentPanel;

    public ReaderListView()
    {
        InitializeComponent();

        SetBinding(OrientationProperty, new Microsoft.UI.Xaml.Data.Binding
        {
            Source = ContentPanel,
            Path = new PropertyPath(nameof(Orientation)),
            Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay
        });

        _layoutCache.Items = ItemsSource;

        ContentPanel.RequestItemRect = index =>
        {
            if (index < 0 || index >= _containerToItemIndexMapper.Count)
            {
                Logger.F(TAG, $"Container {index} not found.");
                return null;
            }

            int itemIndex = _containerToItemIndexMapper[index];
            if (_layoutCache.TryGetItemRect(itemIndex, out Rect rect, Orientation))
            {
                return rect;
            }

            return null;
        };

        ContentPanel.RequestSize = () =>
        {
            return _layoutCache.GetSize(Orientation);
        };
    }

    public bool TryGetItemRect(int index, out Rect rect)
    {
        return _layoutCache.TryGetItemRect(index, out rect, Orientation);
    }

    public void SetVisibleItemIndices(IEnumerable<int> indices)
    {
        ChangeItems(() =>
        {
            _visibleItemIndices.Clear();
            _visibleItemIndices.AddRange(indices);
            HandleItemDiff([.. _containerToItemIndexMapper]);
        });
    }

    protected override void OnStart()
    {
        base.OnStart();
        SubscribeItemsSourceChange();
        RefreshAllItems();
    }

    protected override void OnStop()
    {
        base.OnStop();

        UnsubscribeItemsSourceChange();

        ReaderListViewPanel? panel = ContentPanel;
        if (panel is not null)
        {
            foreach (UIElement item in panel.Children)
            {
                if (item is BaseUserControl lifecyleItem)
                {
                    lifecyleItem.MarkAsStopped();
                }
            }

            panel.Children.Clear();
        }

        foreach (UIElement item in _recycledContainers)
        {
            if (item is BaseUserControl lifecyleItem)
            {
                lifecyleItem.MarkAsStopped();
            }
        }

        _recycledContainers.Clear();
    }

    private void SubscribeItemsSourceChange()
    {
        _collectionChangedSource?.CollectionChanged -= OnItemsCollectionChanged;
        _collectionChangedSource?.CollectionChanged += OnItemsCollectionChanged;
    }

    private void UnsubscribeItemsSourceChange()
    {
        _collectionChangedSource?.CollectionChanged -= OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                HandleItemsAdded(args.NewStartingIndex, args.NewItems?.Count ?? 0);
                break;
            case NotifyCollectionChangedAction.Remove:
                HandleItemsRemoved(args.OldStartingIndex, args.OldItems?.Count ?? 0);
                break;
            case NotifyCollectionChangedAction.Replace:
                HandleItemsReplaced(args.NewStartingIndex, args.OldItems?.Count ?? 0, args.NewItems?.Count ?? 0);
                break;
            case NotifyCollectionChangedAction.Move:
                HandleItemsMoved(args.OldStartingIndex, args.NewStartingIndex, args.NewItems?.Count ?? 0);
                break;
            case NotifyCollectionChangedAction.Reset:
                RefreshAllItems();
                break;
        }
    }

    private void RefreshAllItems()
    {
        ChangeItems(() =>
        {
            _layoutCache.InvalidateCache();
            HandleItemDiff([.. Enumerable.Repeat(-1, _containerToItemIndexMapper.Count)]);
        });
    }

    private void HandleItemsAdded(int baseIndex, int itemCount)
    {
        ChangeItems(() =>
        {
            _layoutCache.InvalidateCache(baseIndex);

            List<int> oldIndices = [.. _containerToItemIndexMapper];
            for (int i = 0; i < oldIndices.Count; i++)
            {
                if (oldIndices[i] < baseIndex)
                {
                    continue;
                }

                oldIndices[i] += itemCount;
            }

            HandleItemDiff(oldIndices);
        });
    }

    private void HandleItemsRemoved(int baseIndex, int itemCount)
    {
        ChangeItems(() =>
        {
            _layoutCache.InvalidateCache(baseIndex);

            List<int> oldIndices = [.. _containerToItemIndexMapper];
            for (int i = 0; i < oldIndices.Count; i++)
            {
                if (oldIndices[i] < baseIndex)
                {
                    continue;
                }

                if (oldIndices[i] < baseIndex + itemCount)
                {
                    oldIndices[i] = -1;
                }
                else
                {
                    oldIndices[i] -= itemCount;
                }
            }

            HandleItemDiff(oldIndices);
        });
    }

    private void HandleItemsReplaced(int baseIndex, int oldCount, int newCount)
    {
        ChangeItems(() =>
        {
            _layoutCache.InvalidateCache(baseIndex);

            List<int> oldIndices = [.. _containerToItemIndexMapper];
            int diff = newCount - oldCount;
            for (int i = 0; i < oldIndices.Count; i++)
            {
                if (oldIndices[i] < baseIndex)
                {
                    continue;
                }

                if (oldIndices[i] < baseIndex + oldCount)
                {
                    oldIndices[i] = -1;
                }
                else
                {
                    oldIndices[i] += diff;
                }
            }

            HandleItemDiff(oldIndices);
        });
    }

    private void HandleItemsMoved(int oldIndex, int newIndex, int itemCount)
    {
        ChangeItems(() =>
        {
            int startIndex = Math.Min(oldIndex, newIndex);
            int endIndex = Math.Max(oldIndex, newIndex) + itemCount;

            _layoutCache.InvalidateCache(startIndex);

            List<int> oldIndices = [.. _containerToItemIndexMapper];
            int diff = newIndex - oldIndex;
            for (int i = 0; i < oldIndices.Count; i++)
            {
                int itemIndex = oldIndices[i];
                if (itemIndex < startIndex || itemIndex >= endIndex)
                {
                    continue;
                }

                if (itemIndex >= oldIndex && itemIndex < oldIndex + itemCount)
                {
                    oldIndices[i] += diff;
                }
                else
                {
                    oldIndices[i] -= diff;
                }
            }

            HandleItemDiff(oldIndices);
        });
    }

    private void ChangeItems(Action action)
    {
        if (_isChanging)
        {
            throw new InvalidOperationException("Cannot start an item change while another change is ongoing.");
        }

        _isChanging = true;
        try
        {
            action();
        }
        finally
        {
            _isChanging = false;
        }
    }

    private void HandleItemDiff(List<int> oldItemIndices)
    {
        ReaderListViewPanel? panel = ContentPanel;
        if (panel is null)
        {
            return;
        }

        IReadOnlyList<IReaderListViewItemViewModel> itemsSource = ItemsSource;
        List<int> added = [.. _visibleItemIndices.Where(i => i < itemsSource.Count)];

        for (int containerIndex = oldItemIndices.Count - 1; containerIndex >= 0; containerIndex--)
        {
            int itemIndex = oldItemIndices[containerIndex];
            if (!added.Remove(itemIndex))
            {
                _containerToItemIndexMapper.RemoveAt(containerIndex);
                UIElement container = panel.Children[containerIndex];
                panel.Children.RemoveAt(containerIndex);
                RecycleContainer(container);
            }
        }

        DataTemplate? template = ItemTemplate;

        foreach (int itemIndex in added)
        {
            _containerToItemIndexMapper.Add(itemIndex);
            IReaderListViewItemViewModel? item = itemsSource[itemIndex];
            UIElement container = GetOrCreateContainer(item, template);
            panel.Children.Add(container);
        }
    }

    private UIElement GetOrCreateContainer(IReaderListViewItemViewModel? item, DataTemplate? template)
    {
        UIElement container;

        if (_recycledContainers.Count > 0)
        {
            container = _recycledContainers.Dequeue();
            if (container is FrameworkElement frameworkElement)
            {
                frameworkElement.DataContext = item;
            }
        }
        else if (template is not null)
        {
            object? content = template.LoadContent();
            if (content is FrameworkElement frameworkElement)
            {
                frameworkElement.DataContext = item;
                container = frameworkElement;
            }
            else if (content is UIElement uiElement)
            {
                container = uiElement;
            }
            else
            {
                throw new InvalidOperationException("The DataTemplate must produce a FrameworkElement or UIElement.");
            }
        }
        else
        {
            var textBlock = new TextBlock { Text = item?.ToString() ?? string.Empty };
            container = textBlock;
        }

        return container;
    }

    private void RecycleContainer(UIElement container)
    {
        if (container is FrameworkElement frameworkElement)
        {
            frameworkElement.DataContext = null;
        }

        _recycledContainers.Enqueue(container);
    }
}
