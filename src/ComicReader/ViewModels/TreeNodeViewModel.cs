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

    public string Glyph { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool CanExpand { get; set; } = false;

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

    public ObservableCollection<TreeNodeViewModel> Children { get; set; } = [];

    public List<BaseMenuFlyoutItemViewModel> MenuFlyoutItems { get; set; } = [];
    public FlyoutBase ContextFlyout
    {
        get
        {
            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemViewModel item in MenuFlyoutItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            return flyout;
        }
    }

    public Action? OnClick;
    public PointerEventHandler? OnPointerPressed => new((sender, e) => OnClick?.Invoke());
}
