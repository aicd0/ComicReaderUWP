// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.ViewModels;

internal partial class TreeNodeViewModel : BaseViewModel, INotifyPropertyChanged
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

    private bool _expanded = false;
    public bool Expanded
    {
        get => _expanded;
        set
        {
            _expanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Expanded)));
        }
    }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    private List<BaseMenuFlyoutItemViewModel> _menuFlyoutItems = [];
    public List<BaseMenuFlyoutItemViewModel> MenuFlyoutItems
    {
        get => _menuFlyoutItems;
        set
        {
            _menuFlyoutItems = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MenuFlyoutItems)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContextFlyout)));
        }
    }

    public FlyoutBase? ContextFlyout
    {
        get
        {
            if (MenuFlyoutItems.Count == 0)
            {
                return null;
            }

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemViewModel item in MenuFlyoutItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            return flyout;
        }
    }

    private Action? _onClick;
    public Action? OnClick
    {
        get => _onClick;
        set
        {
            _onClick = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnClick)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnPointerPressed)));
        }
    }

    public PointerEventHandler? OnPointerPressed => new((sender, e) => OnClick?.Invoke());
}
