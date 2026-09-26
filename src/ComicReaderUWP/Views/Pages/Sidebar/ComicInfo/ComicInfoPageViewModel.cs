// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.Sidebar.ComicInfo;

internal partial class ComicInfoPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        set
        {
            _isEmpty = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
        }
    }

    private bool _isEditable;
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    private string _comicTitle1 = string.Empty;
    public string ComicTitle1
    {
        get => _comicTitle1;
        set
        {
            _comicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle1)));
        }
    }

    private string _comicTitle2 = string.Empty;
    public string ComicTitle2
    {
        get => _comicTitle2;
        set
        {
            _comicTitle2 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTitle2Visible)));
        }
    }

    private double _rating;
    public double Rating
    {
        get => _rating;
        set
        {
            _rating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rating)));
        }
    }

    private string _comicDir = "";
    public string ComicDir
    {
        get => _comicDir;
        set
        {
            _comicDir = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicDir)));
        }
    }

    private bool _hasAnyTags = false;
    public bool HasAnyTags
    {
        get => _hasAnyTags;
        set
        {
            _hasAnyTags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAnyTags)));
        }
    }

    public ComicModel? Comic => _comic;
    public PlaylistModel Playlist { get; set; } = PlaylistModel.CreateEmpty();
    public PlaybackModel? Playback { get; set; }
    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<string> ImageDescriptions { get; } = [];

    public readonly MutableLiveData<string> ComicDescriptionLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<CompletionStatusEnum> CompletionStatusLiveData = new();

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private ComicModel? _comic;

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void SetComic(ComicModel? comic)
    {
        if (_comic == comic)
        {
            return;
        }

        _comic = comic;
        ImageDescriptions.Clear();
        LoadComicInfo();
    }

    public void SetImageDescriptions(IReadOnlyList<string> imageDescriptions)
    {
        ImageDescriptions.Clear();
        foreach (string imageDescription in imageDescriptions)
        {
            ImageDescriptions.Add(imageDescription);
        }
    }

    public bool AddNewTags(string command)
    {
        ComicModel? comic = _comic;
        if (comic is null || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        command = command.ReplaceLineEndings(string.Empty);
        string key = string.Empty;
        string value = command;
        bool overwriteMode = false;
        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];
            if (LocalizationUtils.Commas.Contains(c))
            {
                break;
            }
            else if (LocalizationUtils.Colons.Contains(c))
            {
                key = command[..i];
                overwriteMode = i + 1 < command.Length && LocalizationUtils.Colons.Contains(command[i + 1]);
                value = command[(overwriteMode ? i + 2 : i + 1)..];
                break;
            }
        }

        key = key.Trim();
        if (string.IsNullOrEmpty(key))
        {
            key = StringResourceProvider.Instance.Default;
        }

        string[] values = value.Split(LocalizationUtils.Commas, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
        if (!tags.TryGetValue(key, out HashSet<string>? categoryTags))
        {
            categoryTags = [];
            tags.Add(key, categoryTags);
        }

        if (overwriteMode)
        {
            categoryTags.Clear();
        }

        foreach (string tag in values)
        {
            categoryTags.Add(tag);
        }

        CoroutineUtils.Run(() => comic.SetTags(tags));
        return true;
    }

    public void Reload()
    {
        LoadComicInfo();
    }

    private void LoadComicInfo()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            IsEmpty = true;
            return;
        }

        IsEmpty = false;
        IsExternalComicLiveData.Emit(comic.IsExternal);

        if (comic.Title1.Length == 0)
        {
            ComicTitle1 = comic.Title;
        }
        else
        {
            ComicTitle1 = comic.Title1;
            ComicTitle2 = comic.Title2;
        }

        ComicDescriptionLiveData.Emit(comic.Description);

        ComicDir = comic.Location;
        IsEditable = comic.IsEditable;

        TagCollectionViewModel.Update(ComicTags, comic, _actionHandler);
        HasAnyTags = ComicTags.Count > 0;
        CompletionStatusLiveData.Emit(comic.CompletionStatus);

        if (!comic.IsExternal)
        {
            int rating = comic.Rating;
            Rating = rating >= 0 ? rating * 0.05F : -1.0;
        }
    }
}
