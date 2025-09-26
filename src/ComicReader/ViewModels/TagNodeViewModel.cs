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

internal partial class TagNodeViewModel : BaseViewModel, INotifyPropertyChanged
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

    public ObservableCollection<TagNodeViewModel> Children { get; } = [];

    private Action? _onClick;
    public Action? OnClick
    {
        get => _onClick;
        set
        {
            _onClick = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnClick)));
        }
    }

    public Func<Task<List<BaseMenuFlyoutItemViewModel>>>? OnRequestContextFlyoutAsync { get; set; }

    public async Task<FlyoutBase?> CreateContextFlyout()
    {
        if (OnRequestContextFlyoutAsync is null)
        {
            return null;
        }

        List<BaseMenuFlyoutItemViewModel> menuFlyoutItems = await OnRequestContextFlyoutAsync();
        if (menuFlyoutItems.Count == 0)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemViewModel item in menuFlyoutItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        return flyout;
    }
}
