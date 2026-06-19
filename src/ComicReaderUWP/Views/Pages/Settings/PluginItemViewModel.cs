// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class PluginItemViewModel : BaseViewModel, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    private string _publisher = string.Empty;
    public string Publisher
    {
        get => _publisher;
        set
        {
            _publisher = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Publisher)));
        }
    }

    public bool DescriptionVisible => !string.IsNullOrWhiteSpace(Description);

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescriptionVisible)));
        }
    }

    private IconSource? _icon;
    public IconSource? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    private string _version = string.Empty;
    public string Version
    {
        get => _version;
        set
        {
            _version = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        }
    }

    private string _location = string.Empty;
    public string Location
    {
        get => _location;
        set
        {
            _location = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Location)));
        }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public Func<PluginItemViewModel, List<BaseMenuFlyoutItemModel>>? RequestOperationMenuItems { get; set; }

    public List<BaseMenuFlyoutItemModel> CreateOperationMenuItems()
    {
        if (RequestOperationMenuItems is null)
        {
            return [];
        }

        return RequestOperationMenuItems(this);
    }
}
