// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.ViewModels;

internal partial class TagViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _tag = string.Empty;
    public string Tag
    {
        get => _tag;
        set
        {
            _tag = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tag)));
        }
    }

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

    public Action? OnClicked { get; set; }
};

internal partial class TagCollectionViewModel(string name) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public ObservableCollection<TagViewModel> Tags { get; } = [];
};
