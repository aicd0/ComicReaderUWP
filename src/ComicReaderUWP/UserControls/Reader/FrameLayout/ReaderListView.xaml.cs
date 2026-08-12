// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

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

        view.ContentPanel.Items = list;

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
    private readonly Queue<UIElement> _recycledContainers = [];
    private readonly List<UIElement?> _realizedContainers = [];
    private bool _isChanging = false;
    private bool _changesInvalidated = false;

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
    }

    public bool TryGetItemRect(int index, out Rect rect)
    {
        return ContentPanel.TryGetItemRect(index, out rect);
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

        foreach (UIElement? item in _realizedContainers)
        {
            if (item is BaseUserControl lifecyleItem)
            {
                lifecyleItem.MarkAsStopped();
            }
        }

        _realizedContainers.Clear();

        foreach (UIElement? item in _recycledContainers)
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
        ChangeItems(RefreshAllItemsInternal);
    }

    private void HandleItemsAdded(int baseIndex, int itemCount)
    {
        ChangeItems(panel =>
        {
            panel.InvalidateCache(baseIndex);

            DataTemplate? template = ItemTemplate;
            IReadOnlyList<IReaderListViewItemViewModel> itemsSource = ItemsSource;

            while (_realizedContainers.Count < itemsSource.Count)
            {
                _realizedContainers.Add(null);
            }

            for (int i = itemsSource.Count - itemCount - 1; i >= baseIndex; i--)
            {
                int oldIndex = i;
                int newIndex = i + itemCount;
                UIElement container = ClearRealizedContainer(oldIndex);
                _realizedContainers[newIndex] = container;
            }

            for (int i = 0; i < itemCount; i++)
            {
                int itemIndex = baseIndex + i;
                IReaderListViewItemViewModel? item = itemsSource[itemIndex];
                UIElement container = GetOrCreateContainer(item, template);
                _realizedContainers[itemIndex] = container;
                panel.Children.Insert(itemIndex, container);
            }
        });
    }

    private void HandleItemsRemoved(int baseIndex, int itemCount)
    {
        ChangeItems(panel =>
        {
            panel.InvalidateCache(baseIndex);

            for (int i = itemCount - 1; i >= 0; i--)
            {
                int removeIndex = baseIndex + i;
                UIElement container = ClearRealizedContainer(removeIndex);
                _realizedContainers.RemoveAt(removeIndex);
                RecycleContainer(container);
                panel.Children.RemoveAt(removeIndex);
            }
        });
    }

    private void HandleItemsReplaced(int baseIndex, int oldCount, int newCount)
    {
        ChangeItems(panel =>
        {
            panel.InvalidateCache(baseIndex);

            DataTemplate? template = ItemTemplate;
            IReadOnlyList<IReaderListViewItemViewModel> itemsSource = ItemsSource;

            int count = Math.Min(oldCount, newCount);
            for (int i = 0; i < count; i++)
            {
                int index = baseIndex + i;
                UIElement container = ClearRealizedContainer(index);
                RecycleContainer(container);
                IReaderListViewItemViewModel? item = itemsSource[index];
                UIElement newContainer = GetOrCreateContainer(item, template);
                _realizedContainers[index] = newContainer;
                panel.Children[index] = newContainer;
            }

            if (oldCount > newCount)
            {
                for (int i = oldCount - 1; i >= count; i--)
                {
                    int removeIndex = baseIndex + i;
                    UIElement container = ClearRealizedContainer(removeIndex);
                    _realizedContainers.RemoveAt(removeIndex);
                    RecycleContainer(container);
                    panel.Children.RemoveAt(removeIndex);
                }
            }
            else
            {
                for (int i = count; i < newCount; i++)
                {
                    int itemIndex = baseIndex + i;
                    IReaderListViewItemViewModel? item = itemsSource[itemIndex];
                    UIElement container = GetOrCreateContainer(item, template);
                    _realizedContainers.Insert(itemIndex, container);
                    panel.Children.Insert(itemIndex, container);
                }
            }
        });
    }

    private void HandleItemsMoved(int oldIndex, int newIndex, int itemCount)
    {
        ChangeItems(panel =>
        {
            panel.InvalidateCache(Math.Min(oldIndex, newIndex));

            int indexDiff = Math.Abs(oldIndex - newIndex);
            if (indexDiff < itemCount)
            {
                (oldIndex, newIndex) = (newIndex, oldIndex);
                (indexDiff, itemCount) = (itemCount, indexDiff);
            }

            if (newIndex > oldIndex)
            {
                int targetIndex = newIndex + indexDiff - 1;
                for (int i = itemCount - 1; i >= 0; i--)
                {
                    int sourceIndex = oldIndex + i;
                    UIElement container = ClearRealizedContainer(sourceIndex);
                    _realizedContainers.RemoveAt(sourceIndex);
                    panel.Children.RemoveAt(sourceIndex);
                    _realizedContainers.Insert(targetIndex, container);
                    panel.Children.Insert(targetIndex, container);
                }
            }
            else
            {
                for (int i = itemCount - 1; i >= 0; i--)
                {
                    int sourceIndex = oldIndex + i;
                    int targetIndex = newIndex + i;
                    UIElement container = ClearRealizedContainer(sourceIndex);
                    _realizedContainers.RemoveAt(sourceIndex);
                    panel.Children.RemoveAt(sourceIndex);
                    _realizedContainers.Insert(targetIndex, container);
                    panel.Children.Insert(targetIndex, container);
                }
            }
        });
    }

    private void ChangeItems(Action<ReaderListViewPanel> action)
    {
        ReaderListViewPanel? panel = ContentPanel;
        if (panel is null)
        {
            return;
        }

        if (_isChanging)
        {
            Logger.F(TAG, "Reentered ChangeItems.");
            _changesInvalidated = true;
            return;
        }

        _isChanging = true;
        try
        {
            do
            {
                _changesInvalidated = false;

                if (_realizedContainers.Count != panel.Children.Count)
                {
                    Logger.F(TAG, $"Inconsistency detected before items change (containers=${_realizedContainers.Count}, children=${panel.Children.Count})");
                }

                action(panel);

                if (_realizedContainers.Count != panel.Children.Count || _realizedContainers.Count != ItemsSource.Count)
                {
                    Logger.F(TAG, $"Inconsistency detected after items change (items=${ItemsSource.Count}, containers=${_realizedContainers.Count}, children=${panel.Children.Count})");
                }

                action = RefreshAllItemsInternal;
            } while (_changesInvalidated);
        }
        finally
        {
            _isChanging = false;
        }
    }

    private void RefreshAllItemsInternal(ReaderListViewPanel panel)
    {
        panel.InvalidateCache();

        for (int i = 0; i < _realizedContainers.Count; i++)
        {
            UIElement container = ClearRealizedContainer(i);
            RecycleContainer(container);
        }

        _realizedContainers.Clear();
        panel.Children.Clear();

        DataTemplate? template = ItemTemplate;
        IReadOnlyList<IReaderListViewItemViewModel> itemsSource = ItemsSource;

        for (int i = 0; i < itemsSource.Count; i++)
        {
            IReaderListViewItemViewModel? item = itemsSource[i];
            UIElement container = GetOrCreateContainer(item, template);
            _realizedContainers.Add(container);
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

    private UIElement ClearRealizedContainer(int index)
    {
        UIElement container = GetRealizedContainer(index);
        _realizedContainers[index] = null;
        return container;
    }

    private UIElement GetRealizedContainer(int index)
    {
        return _realizedContainers[index] ?? throw new InvalidOperationException($"No realized container found for index {index}.");
    }
}
