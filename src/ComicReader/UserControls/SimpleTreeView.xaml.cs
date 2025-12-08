// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

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

    public ObservableCollection<TagNodeViewModel> _dataSource { get; set; } = [];
    public ObservableCollection<TagNodeViewModel> DataSource
    {
        get => _dataSource;
        set
        {
            _dataSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataSource)));
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
        var item = (TagNodeViewModel)args.InvokedItem;
        item.OnClick?.Invoke();
    }

    private async void TreeView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.OriginalSource is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not TagNodeViewModel viewModel)
        {
            return;
        }

        FlyoutBase? flyout = await viewModel.CreateContextFlyout();
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
