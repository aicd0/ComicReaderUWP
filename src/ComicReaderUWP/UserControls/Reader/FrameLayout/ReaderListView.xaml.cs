// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

public sealed partial class ReaderListView : UserControl
{
    private INotifyCollectionChanged? _collectionChangedSource;
    private readonly Queue<UIElement> _recycledContainers = new();
    private readonly Dictionary<int, UIElement> _realizedContainers = new();

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(ReaderListView), new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ReaderListView), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ReaderListView), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public event EventHandler<CustomContainerContentChangingEventArgs>? ContainerContentChanging;

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

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReaderListView listView)
        {
            listView.RefreshAllItems();
        }
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReaderListView listView)
        {
            if (listView._collectionChangedSource != null)
            {
                listView._collectionChangedSource.CollectionChanged -= listView.OnItemsCollectionChanged;
                listView._collectionChangedSource = null;
            }

            if (e.NewValue is INotifyCollectionChanged notifyCollection)
            {
                listView._collectionChangedSource = notifyCollection;
                listView._collectionChangedSource.CollectionChanged += listView.OnItemsCollectionChanged;
            }

            listView.RefreshAllItems();
        }
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReaderListView listView)
        {
            listView.RefreshAllItems();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                HandleItemsAdded(args.NewStartingIndex, args.NewItems);
                break;

            case NotifyCollectionChangedAction.Remove:
                HandleItemsRemoved(args.OldStartingIndex, args.OldItems);
                break;

            case NotifyCollectionChangedAction.Replace:
                HandleItemsReplaced(args.NewStartingIndex, args.OldItems, args.NewItems);
                break;

            case NotifyCollectionChangedAction.Move:
                HandleItemsMoved(args.OldStartingIndex, args.NewStartingIndex, args.NewItems);
                break;

            case NotifyCollectionChangedAction.Reset:
                RefreshAllItems();
                break;
        }
    }

    private void RefreshAllItems()
    {
        if (ContentPanel == null)
        {
            return;
        }

        ContentPanel.Children.Clear();
        _realizedContainers.Clear();
        ContentPanel.Orientation = Orientation;

        DataTemplate? template = ItemTemplate;
        IEnumerable itemsSource = ItemsSource ?? Array.Empty<object>();

        int itemIndex = 0;
        foreach (object? item in itemsSource)
        {
            UIElement? containerElement = GetOrCreateContainer(itemIndex, item, template, inRecycleQueue: false);
            if (containerElement != null)
            {
                ContentPanel.Children.Add(containerElement);
                _realizedContainers[itemIndex] = containerElement;
            }

            itemIndex++;
        }
    }

    private void HandleItemsAdded(int baseIndex, IList? items)
    {
        if (ContentPanel == null || items == null || items.Count == 0)
        {
            return;
        }

        DataTemplate? template = ItemTemplate;

        for (int i = 0; i < items.Count; i++)
        {
            int itemIndex = baseIndex + i;
            object? item = items[i];

            ShiftRealizedContainersAfter(itemIndex - 1);

            UIElement? containerElement = GetOrCreateContainer(itemIndex, item, template, inRecycleQueue: false);
            if (containerElement != null)
            {
                ContentPanel.Children.Insert(itemIndex, containerElement);
                _realizedContainers[itemIndex] = containerElement;
            }
        }
    }

    private void HandleItemsRemoved(int baseIndex, IList? items)
    {
        if (ContentPanel == null || items == null || items.Count == 0)
        {
            return;
        }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            int itemIndex = baseIndex + i;

            if (_realizedContainers.TryGetValue(itemIndex, out UIElement? container))
            {
                ContentPanel.Children.RemoveAt(itemIndex);
                _realizedContainers.Remove(itemIndex);
                RecycleContainer(itemIndex, container);
            }
        }

        ShiftRealizedContainersAfter(baseIndex - 1);
    }

    private void HandleItemsReplaced(int baseIndex, IList? oldItems, IList? newItems)
    {
        if (ContentPanel == null || newItems == null || newItems.Count == 0)
        {
            return;
        }

        DataTemplate? template = ItemTemplate;

        for (int i = 0; i < newItems.Count; i++)
        {
            int itemIndex = baseIndex + i;
            object? newItem = newItems[i];

            if (_realizedContainers.TryGetValue(itemIndex, out UIElement? oldContainer))
            {
                RecycleContainer(itemIndex, oldContainer);
                _realizedContainers.Remove(itemIndex);
            }

            UIElement? newContainerElement = GetOrCreateContainer(itemIndex, newItem, template, inRecycleQueue: false);
            if (newContainerElement != null)
            {
                ContentPanel.Children[itemIndex] = newContainerElement;
                _realizedContainers[itemIndex] = newContainerElement;
            }
        }
    }

    private void HandleItemsMoved(int oldIndex, int newIndex, IList? items)
    {
        if (ContentPanel == null || items == null || items.Count == 0)
        {
            return;
        }

        if (_realizedContainers.TryGetValue(oldIndex, out UIElement? container))
        {
            ContentPanel.Children.RemoveAt(oldIndex);
            ContentPanel.Children.Insert(newIndex, container);

            _realizedContainers.Remove(oldIndex);
            _realizedContainers[newIndex] = container;

            ShiftRealizedContainers(Math.Min(oldIndex, newIndex), Math.Max(oldIndex, newIndex));
        }
    }

    private UIElement? GetOrCreateContainer(int itemIndex, object? item, DataTemplate? template, bool inRecycleQueue)
    {
        UIElement? containerElement = null;

        if (_recycledContainers.Count > 0)
        {
            containerElement = _recycledContainers.Dequeue();
            if (containerElement is FrameworkElement frameworkElement)
            {
                frameworkElement.DataContext = item;
            }
        }
        else if (template != null)
        {
            try
            {
                object? content = template.LoadContent();
                if (content is FrameworkElement frameworkElement)
                {
                    frameworkElement.DataContext = item;
                    containerElement = frameworkElement;
                }
                else if (content is UIElement uiElement)
                {
                    containerElement = uiElement;
                }
            }
            catch
            {
                containerElement = null;
            }
        }

        if (containerElement == null && template == null)
        {
            var textBlock = new TextBlock { Text = item?.ToString() ?? string.Empty };
            containerElement = textBlock;
        }

        if (containerElement != null)
        {
            RaiseContainerContentChanging(itemIndex, item, containerElement, inRecycleQueue);
        }

        return containerElement;
    }

    private void RecycleContainer(int itemIndex, UIElement container)
    {
        _recycledContainers.Enqueue(container);
        RaiseContainerContentChanging(itemIndex, null, container, inRecycleQueue: true);
    }

    private void ShiftRealizedContainers(int startIndex, int endIndex)
    {
        var keysToUpdate = new List<int>();
        foreach (int key in _realizedContainers.Keys)
        {
            if (key >= startIndex && key <= endIndex)
            {
                keysToUpdate.Add(key);
            }
        }

        foreach (int key in keysToUpdate)
        {
            if (_realizedContainers.TryGetValue(key, out UIElement? container))
            {
                _realizedContainers.Remove(key);
                int newKey = key + (key < startIndex ? -1 : 1);
                _realizedContainers[newKey] = container;
            }
        }
    }

    private void ShiftRealizedContainersAfter(int index)
    {
        var keysToUpdate = new List<int>();
        foreach (int key in _realizedContainers.Keys)
        {
            if (key > index)
            {
                keysToUpdate.Add(key);
            }
        }

        foreach (int key in keysToUpdate)
        {
            if (_realizedContainers.TryGetValue(key, out UIElement? container))
            {
                _realizedContainers.Remove(key);
                _realizedContainers[key + 1] = container;
            }
        }
    }

    private void RaiseContainerContentChanging(int itemIndex, object? item, UIElement itemContainer, bool inRecycleQueue)
    {
        ContainerContentChanging?.Invoke(this, new CustomContainerContentChangingEventArgs(itemIndex, item, itemContainer, inRecycleQueue));
    }
}
