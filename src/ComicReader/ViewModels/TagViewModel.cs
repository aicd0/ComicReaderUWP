// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

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

    public Action? OnClicked { get; set; }
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
};

internal partial class TagCollectionViewModel(string name) : INotifyPropertyChanged
{
    public static readonly TagCollectionViewModel Default = new(string.Empty);

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

    public void NotifyImmediately()
    {
        for (int i = 0; i < Tags.Count; i++)
        {
            Tags[i] = Tags[i];
        }
    }
};
