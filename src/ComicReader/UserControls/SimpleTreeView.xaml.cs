// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.BaseUI;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.UserControls;

internal sealed partial class SimpleTreeView : BaseUserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SimpleTreeViewNodeModel> _dataSource = [];
    public ObservableCollection<SimpleTreeViewNodeModel> DataSource
    {
        get => _dataSource;
        set
        {
            _dataSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataSource)));
        }
    }

    public bool SelectionMode
    {
        get { return (bool)GetValue(SelectionModeProperty); }
        set { SetValue(SelectionModeProperty, value); }
    }
    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(nameof(SelectionMode), typeof(bool), typeof(SimpleTreeView), new PropertyMetadata(false, OnSelectionModeChanged));

    private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (SimpleTreeView)d;
        bool selectionMode = self.SelectionMode;
        self.MainTreeView.SelectionMode = selectionMode ? TreeViewSelectionMode.Multiple : TreeViewSelectionMode.Single;
        if (selectionMode)
        {
            self.MainTreeView.SelectedItems.Clear();
        }
    }

    public SimpleTreeView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            Bindings.StopTracking();
            Bindings.Update();
        };
    }

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        SelectionMode = false;
        var item = (SimpleTreeViewNodeModel)args.InvokedItem;
        item.Clicked?.Invoke();
    }

    private void TreeView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        SelectionMode = false;
    }

    private void TreeViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private async void TreeView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.OriginalSource is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not SimpleTreeViewNodeModel viewModel)
        {
            return;
        }

        List<SimpleTreeViewNodeModel> selectedItems = [];
        foreach (object? item in MainTreeView.SelectedItems)
        {
            selectedItems.Add((SimpleTreeViewNodeModel)item);
        }

        FlyoutBase? flyout = await viewModel.CreateContextFlyout(selectedItems);
        if (flyout is null)
        {
            return;
        }

        if (args.TryGetPosition(fe, out Windows.Foundation.Point point))
        {
            flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
        }
        else
        {
            flyout.ShowAt(fe);
        }

        args.Handled = true;
    }
}
