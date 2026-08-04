// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

public sealed partial class ReaderListView : UserControl
{
    private INotifyCollectionChanged? _collectionChangedSource;
    private readonly Queue<UIElement> _recycledContainers = [];
    private readonly List<UIElement?> _realizedContainers = [];

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(ReaderListView), new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IList), typeof(ReaderListView), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ReaderListView), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
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

    public Rect GetItemRect(int index)
    {
        return ContentPanel.GetItemRect(index);
    }

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView listView)
        {
            return;
        }

        listView.RefreshAllItems();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView listView)
        {
            return;
        }

        if (e.NewValue is not IReadOnlyList<IReaderListViewItemViewModel> list)
        {
            throw new InvalidOperationException("ItemSource has to be convertable to IReadOnlyList<IReaderListViewItemViewModel>.");
        }

        listView.ContentPanel.Items = list;

        listView._collectionChangedSource?.CollectionChanged -= listView.OnItemsCollectionChanged;
        listView._collectionChangedSource = null;

        if (e.NewValue is INotifyCollectionChanged notifyCollection)
        {
            listView._collectionChangedSource = notifyCollection;
            listView._collectionChangedSource.CollectionChanged += listView.OnItemsCollectionChanged;
        }

        listView.RefreshAllItems();
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ReaderListView listView)
        {
            return;
        }

        listView.RefreshAllItems();
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
        if (ContentPanel is null)
        {
            return;
        }

        ContentPanel.InvalidateCache();

        for (int i = 0; i < _realizedContainers.Count; i++)
        {
            UIElement container = ClearRealizedContainer(i);
            RecycleContainer(container);
        }

        _realizedContainers.Clear();
        ContentPanel.Children.Clear();

        DataTemplate? template = ItemTemplate;
        IList itemsSource = ItemsSource ?? Array.Empty<object>();

        for (int i = 0; i < itemsSource.Count; i++)
        {
            object? item = itemsSource[i];
            UIElement container = GetOrCreateContainer(item, template);
            _realizedContainers.Add(container);
            ContentPanel.Children.Add(container);
        }
    }

    private void HandleItemsAdded(int baseIndex, int itemCount)
    {
        if (ContentPanel is null)
        {
            return;
        }

        ContentPanel.InvalidateCache(baseIndex);

        DataTemplate? template = ItemTemplate;
        IList itemsSource = ItemsSource ?? Array.Empty<object>();

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
            object? item = itemsSource[itemIndex];
            UIElement container = GetOrCreateContainer(item, template);
            _realizedContainers[itemIndex] = container;
            ContentPanel.Children.Insert(itemIndex, container);
        }
    }

    private void HandleItemsRemoved(int baseIndex, int itemCount)
    {
        if (ContentPanel is null)
        {
            return;
        }

        ContentPanel.InvalidateCache(baseIndex);

        for (int i = itemCount - 1; i >= 0; i--)
        {
            int removeIndex = baseIndex + i;
            UIElement container = ClearRealizedContainer(removeIndex);
            _realizedContainers.RemoveAt(removeIndex);
            RecycleContainer(container);
            ContentPanel.Children.RemoveAt(removeIndex);
        }
    }

    private void HandleItemsReplaced(int baseIndex, int oldCount, int newCount)
    {
        if (ContentPanel is null)
        {
            return;
        }

        ContentPanel.InvalidateCache(baseIndex);

        DataTemplate? template = ItemTemplate;
        IList itemsSource = ItemsSource ?? Array.Empty<object>();

        int count = Math.Min(oldCount, newCount);
        for (int i = 0; i < count; i++)
        {
            int index = baseIndex + i;
            UIElement container = ClearRealizedContainer(index);
            RecycleContainer(container);
            object? item = itemsSource[index];
            UIElement newContainer = GetOrCreateContainer(item, template);
            _realizedContainers[index] = newContainer;
            ContentPanel.Children[index] = newContainer;
        }

        if (oldCount > newCount)
        {
            for (int i = oldCount - 1; i >= count; i--)
            {
                int removeIndex = baseIndex + i;
                UIElement container = ClearRealizedContainer(removeIndex);
                _realizedContainers.RemoveAt(removeIndex);
                RecycleContainer(container);
                ContentPanel.Children.RemoveAt(removeIndex);
            }
        }
        else
        {
            for (int i = count; i < newCount; i++)
            {
                int itemIndex = baseIndex + i;
                object? item = itemsSource[itemIndex];
                UIElement container = GetOrCreateContainer(item, template);
                _realizedContainers.Insert(itemIndex, container);
                ContentPanel.Children.Insert(itemIndex, container);
            }
        }
    }

    private void HandleItemsMoved(int oldIndex, int newIndex, int itemCount)
    {
        if (ContentPanel is null)
        {
            return;
        }

        ContentPanel.InvalidateCache(Math.Min(oldIndex, newIndex));

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
                ContentPanel.Children.RemoveAt(sourceIndex);
                _realizedContainers.Insert(targetIndex, container);
                ContentPanel.Children.Insert(targetIndex, container);
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
                ContentPanel.Children.RemoveAt(sourceIndex);
                _realizedContainers.Insert(targetIndex, container);
                ContentPanel.Children.Insert(targetIndex, container);
            }
        }
    }

    private UIElement GetOrCreateContainer(object? item, DataTemplate? template)
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
