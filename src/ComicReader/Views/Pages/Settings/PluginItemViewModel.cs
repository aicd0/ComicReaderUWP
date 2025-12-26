// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;

namespace ComicReader.Views.Pages.Settings;

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
