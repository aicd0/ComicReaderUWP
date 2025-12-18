// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.ViewModels;

internal partial class SimpleTreeViewNodeModel : BaseViewModel, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _glyph = string.Empty;
    public string Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Glyph)));
        }
    }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
        }
    }

    private bool _canExpand = false;
    public bool CanExpand
    {
        get => _canExpand;
        set
        {
            _canExpand = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanExpand)));
        }
    }

    private bool _isExpanded = false;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public ObservableCollection<SimpleTreeViewNodeModel> Children { get; } = [];
    public object? DataContext { get; set; }
    public Action? Clicked { get; set; }
    public Func<SimpleTreeViewNodeModel, IEnumerable<SimpleTreeViewNodeModel>, Task<List<BaseMenuFlyoutItemModel>>>? RequestContextMenuItemsAsync { get; set; }

    public async Task<FlyoutBase?> CreateContextFlyout(IEnumerable<SimpleTreeViewNodeModel> selectedItems)
    {
        if (RequestContextMenuItemsAsync is null)
        {
            return null;
        }

        List<BaseMenuFlyoutItemModel> menuFlyoutItems = await RequestContextMenuItemsAsync(this, selectedItems);
        if (menuFlyoutItems.Count == 0)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in menuFlyoutItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        return flyout;
    }

    public void ExpandAll()
    {
        void helper(SimpleTreeViewNodeModel node)
        {
            if (!node.CanExpand)
            {
                return;
            }

            node.IsExpanded = true;
            foreach (SimpleTreeViewNodeModel child in node.Children)
            {
                helper(child);
            }
        }

        helper(this);
    }

    public void CollapseAll()
    {
        void helper(SimpleTreeViewNodeModel node)
        {
            if (!node.CanExpand)
            {
                return;
            }

            node.IsExpanded = false;
            foreach (SimpleTreeViewNodeModel child in node.Children)
            {
                helper(child);
            }
        }

        helper(this);
    }

    public IEnumerable<T> CollectDataContext<T>()
    {
        IEnumerable<T> helper(SimpleTreeViewNodeModel node)
        {
            {
                if (node.DataContext is T dataContext)
                {
                    yield return dataContext;
                }
            }

            foreach (SimpleTreeViewNodeModel child in node.Children)
            {
                foreach (T dataContext in helper(child))
                {
                    yield return dataContext;
                }
            }
        }

        return helper(this);
    }
}
