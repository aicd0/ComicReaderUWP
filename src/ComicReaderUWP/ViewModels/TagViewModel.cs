// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.ViewModels;

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

    public Action? OnClicked { get; set; }
    public Func<Task<List<BaseMenuFlyoutItemModel>>>? OnRequestContextFlyoutAsync { get; set; }

    public async Task<FlyoutBase?> CreateContextFlyout()
    {
        if (OnRequestContextFlyoutAsync is null)
        {
            return null;
        }

        List<BaseMenuFlyoutItemModel> menuFlyoutItems = await OnRequestContextFlyoutAsync();
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
};
