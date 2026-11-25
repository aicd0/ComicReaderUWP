// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
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

    private bool _isTargetSelectorVisible = false;
    public bool IsTargetSelectorVisible
    {
        get => _isTargetSelectorVisible;
        private set
        {
            _isTargetSelectorVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTargetSelectorVisible)));
        }
    }

    private int _selectedTargetIndex = -1;
    public int SelectedTargetIndex
    {
        get => _selectedTargetIndex;
        private set
        {
            _selectedTargetIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTargetIndex)));
        }
    }

    private bool _isComic = false;
    public bool IsComic
    {
        get => _isComic;
        private set
        {
            _isComic = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComic)));
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
        }
    }

    private string _releaseDateText = string.Empty;
    public string ReleaseDateText
    {
        get => _releaseDateText;
        set
        {
            _releaseDateText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReleaseDateText)));
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

    private string? _coverImageUri = null;
    public string? CoverImageUri
    {
        get => _coverImageUri;
        private set
        {
            _coverImageUri = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CoverImageUri)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCoverImageVisible)));
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

    public ComicModel? Comic => _selectedComic;
    public PlaylistModel Playlist { get; set; } = PlaylistModel.CreateEmpty();
    public PlaybackModel? Playback { get; set; }
    public bool IsCoverImageVisible => !string.IsNullOrEmpty(CoverImageUri);
    public ObservableCollection<ComicModel> Targets { get; } = [];
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<string> ImageDescriptions { get; } = [];

    public readonly MutableLiveData<string> ComicDescriptionLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<CompletionStatusEnum> CompletionStatusLiveData = new();

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private ComicModel? _rootComic;
    private ComicModel? _selectedComic;
    private IReadOnlyList<string> _imageDescriptions = [];
    private List<ComicModel> _containingCollections = [];

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void SetComic(ComicModel? comic)
    {
        if (_rootComic == comic)
        {
            return;
        }

        _rootComic = comic;
        _containingCollections = [];
        RebuildTargets();
        RefreshTargets();
    }

    public void SetImageDescriptions(IReadOnlyList<string> imageDescriptions)
    {
        _imageDescriptions = imageDescriptions;
        UpdateImageDescriptions();
    }

    public void OnComicUpdated(IEnumerable<long> ids)
    {
        HashSet<long> updatedIds = [.. ids];
        if (!Targets.Any(x => updatedIds.Contains(x.Id)))
        {
            return;
        }

        RebuildTargets();
    }

    public void RefreshTargets()
    {
        ComicModel? root = _rootComic;
        if (root is null || root.Id < 0)
        {
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            IReadOnlySet<long> collectionIds = await CollectionModel.GetCollectionIdsContainingAll((List<long>)[root.Id]);
            List<ComicModel> collections = await ComicModel.BatchFromId(collectionIds);
            collections.Sort(CompareByTitle);
            await MainThreadUtils.RunInMainThread(() =>
            {
                if (_rootComic != root)
                {
                    return;
                }

                _containingCollections = collections;
                RebuildTargets();
            });
        });
    }

    public void SetSelectedTargetIndex(int index)
    {
        if (index < 0 || index >= Targets.Count || index == _selectedTargetIndex)
        {
            return;
        }

        SelectTarget(Targets[index]);
    }

    public bool AddNewTags(string command)
    {
        ComicModel? comic = _selectedComic;
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

    private void RebuildTargets()
    {
        ComicModel? selected = _selectedComic;
        Targets.Clear();
        if (_rootComic is { } root)
        {
            Targets.Add(root);
            foreach (ComicModel collection in _containingCollections)
            {
                Targets.Add(collection);
            }
        }

        SelectedTargetIndex = -1;
        SelectTarget(selected);
        IsTargetSelectorVisible = Targets.Count > 1;
    }

    private void SelectTarget(ComicModel? target)
    {
        int index = target is null ? -1 : Targets.IndexOf(target);
        if (index < 0)
        {
            target = Targets.Count > 0 ? Targets[0] : null;
            index = target is null ? -1 : 0;
        }

        if (_selectedComic == target && _selectedTargetIndex == index)
        {
            return;
        }

        _selectedComic = target;
        SelectedTargetIndex = index;
        LoadComicInfo();
    }

    private void LoadComicInfo()
    {
        ComicModel? comic = _selectedComic;
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
            ComicTitle2 = string.Empty;
        }
        else
        {
            ComicTitle1 = comic.Title1;
            ComicTitle2 = comic.Title2;
        }

        ReleaseDateText = comic.ReleaseDate is { } releaseDate ?
            releaseDate.ToString("d", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo()) :
            string.Empty;

        ComicDescriptionLiveData.Emit(comic.Description);

        ComicDir = comic.Location;
        IsComic = !comic.IsCollection;
        CoverImageUri = ComicExt.GetCoverImageUri(comic);
        UpdateImageDescriptions();
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

    private void UpdateImageDescriptions()
    {
        ImageDescriptions.Clear();
        if (_selectedComic != _rootComic)
        {
            return;
        }

        foreach (string imageDescription in _imageDescriptions)
        {
            ImageDescriptions.Add(imageDescription);
        }
    }

    private static int CompareByTitle(ComicModel a, ComicModel b)
    {
        int result = string.Compare(a.Title, b.Title, ignoreCase: true);
        return result != 0 ? result : a.Id.CompareTo(b.Id);
    }
}
