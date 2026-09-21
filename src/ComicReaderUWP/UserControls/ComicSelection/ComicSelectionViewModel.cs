// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.ComicSelection;

internal sealed partial class ComicSelectionViewModel(Func<int> totalItemCountProvider) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly List<ComicItemViewModel> _selectedItems = [];

    private bool _isSelectMode = false;
    public bool IsSelectMode
    {
        get => _isSelectMode;
        set
        {
            _isSelectMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectMode)));
        }
    }

    private ListViewSelectionMode _comicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => _comicItemSelectionMode;
        set
        {
            _comicItemSelectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicItemSelectionMode)));
        }
    }

    private bool _isAnyComicSelected = false;
    public bool IsAnyComicSelected
    {
        get => _isAnyComicSelected;
        set
        {
            _isAnyComicSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAnyComicSelected)));
        }
    }

    private bool _isCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => _isCommandBarSelectAllToggled;
        set
        {
            _isCommandBarSelectAllToggled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarSelectAllToggled)));
        }
    }

    private bool _isCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => _isCommandBarFavoriteEnabled;
        set
        {
            _isCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarFavoriteEnabled)));
        }
    }

    private bool _isCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => _isCommandBarUnFavoriteEnabled;
        set
        {
            _isCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnFavoriteEnabled)));
        }
    }

    private bool _isCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => _isCommandBarHideEnabled;
        set
        {
            _isCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarHideEnabled)));
        }
    }

    private bool _isCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => _isCommandBarUnHideEnabled;
        set
        {
            _isCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnHideEnabled)));
        }
    }

    //
    // Selection
    //

    public void SetSelectMode(bool enabled)
    {
        if (IsSelectMode == enabled)
        {
            return;
        }

        IsSelectMode = enabled;
        ComicItemSelectionMode = enabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
        if (enabled)
        {
            _selectedItems.Clear();
            UpdateCommandBarButtonStates();
        }
    }

    public void SetSelection(IEnumerable<ComicItemViewModel> selectedItems)
    {
        _selectedItems.Clear();
        _selectedItems.AddRange(selectedItems);
        UpdateCommandBarButtonStates();
    }

    public IReadOnlyList<ComicModel> GetSelectedComics()
    {
        return [.. _selectedItems.Select(x => x.Comic)];
    }

    public void UpdateCommandBarButtonStates()
    {
        IEnumerable<ComicItemViewModel> selectedItems = _selectedItems.DistinctBy(x => x.Comic);
        bool allSelected = selectedItems.Count() == totalItemCountProvider();
        bool anySelected = false;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;

        foreach (ComicItemViewModel item in selectedItems)
        {
            anySelected = true;

            if (item.IsFavorite)
            {
                unfavoriteEnabled = true;
            }
            else
            {
                favoriteEnabled = true;
            }

            if (item.IsHide)
            {
                unhideEnabled = true;
            }
            else
            {
                hideEnabled = true;
            }
        }

        IsAnyComicSelected = anySelected;
        IsCommandBarSelectAllToggled = allSelected;
        IsCommandBarFavoriteEnabled = favoriteEnabled;
        IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
        IsCommandBarHideEnabled = hideEnabled;
        IsCommandBarUnHideEnabled = unhideEnabled;
    }
}
