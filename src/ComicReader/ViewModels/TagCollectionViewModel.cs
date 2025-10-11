// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.BaseUI;

namespace ComicReader.ViewModels;

internal partial class TagCollectionViewModel(string name) : BaseViewModel, INotifyPropertyChanged
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

    public override void NotifyImmediately()
    {
        for (int i = 0; i < Tags.Count; i++)
        {
            Tags[i] = Tags[i];
        }
    }
};
