// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.UserControls.ComicSelection;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.Collection;

internal partial class CollectionPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(CollectionPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    //
    // Properties
    //

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

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDescriptionVisible)));
        }
    }

    public bool IsDescriptionVisible => Description.Length > 0;

    private double _rating = -1.0;
    public double Rating
    {
        get => _rating;
        set
        {
            _rating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rating)));
        }
    }

    private string? _coverImageUri = null;
    public string? CoverImageUri
    {
        get => _coverImageUri;
        set
        {
            _coverImageUri = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CoverImageUri)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCoverImageVisible)));
        }
    }

    public bool IsCoverImageVisible => !string.IsNullOrEmpty(CoverImageUri);

    private string? _backgroundImageUri = null;
    public string? BackgroundImageUri
    {
        get => _backgroundImageUri;
        set
        {
            _backgroundImageUri = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundImageUri)));
        }
    }

    private bool _isEditable = false;
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    private bool _isFavorite = false;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            _isFavorite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FavoriteButtonText)));
        }
    }

    public string FavoriteButtonText => IsFavorite
        ? StringResourceProvider.Instance.RemoveFromFavorites
        : StringResourceProvider.Instance.AddToFavorites;

    private int _itemCount = 0;
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (_itemCount == value)
            {
                return;
            }

            _itemCount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemCountText)));
        }
    }

    public string ItemCountText => StringResourceProvider.Instance.NItems.Replace("$n", ItemCount.ToString());

    private ComicFilterModel.ViewTypeEnum _viewType = ComicFilterModel.ViewTypeEnum.Medium;
    public ComicFilterModel.ViewTypeEnum ViewType
    {
        get => _viewType;
        set
        {
            if (_viewType == value)
            {
                return;
            }

            _viewType = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewType)));
        }
    }

    public ComicModel? Collection => _collection;
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ComicSelectionViewModel ComicSelection { get; }

    public readonly MutableLiveData<IReadOnlyList<ComicItemViewModel>> ResultsLiveData = new();
    public readonly MutableLiveData<string> TitleLiveData = new();

    //
    // Fields
    //

    private readonly ComicSearchEngine _searchEngine = new();
    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.Factory.NewQueue("CollectionPageQueue");

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private long _collectionId = -1;
    private ComicModel? _collection;
    private List<ComicModel> _members = [];
    private HashSet<long> _memberIds = [];
    private List<ComicModel> _comics = [];
    private long _lastSearchTime = 0;
    private int _reloadSubmitted = 0;
    private int _reloadInvalidated = 0;

    public CollectionPageViewModel()
    {
        ComicSelection = new(() => _comics.Count);
    }

    //
    // Lifecycle
    //

    public void Initialize(ActionHandler actionHandler, long collectionId)
    {
        _actionHandler = actionHandler;
        _collectionId = collectionId;
        _searchEngine.SetResultCallback(OnSearchResult);
        _searchEngine.IncludeHidden = true;

        Refresh();
    }

    public void Refresh()
    {
        Interlocked.Exchange(ref _reloadInvalidated, 1);
        if (Interlocked.CompareExchange(ref _reloadSubmitted, 1, 0) == 1)
        {
            return;
        }

        _sharedDispatcher.SubmitAsync(async () =>
        {
            Interlocked.Exchange(ref _reloadSubmitted, 0);
            while (Interlocked.Exchange(ref _reloadInvalidated, 0) == 1)
            {
                await ReloadNoLock();
            }
        });
    }

    //
    // Search
    //

    public void SetSearchText(string searchText)
    {
        searchText = searchText.Trim();
        if (searchText == _searchEngine.SearchText)
        {
            return;
        }

        _searchEngine.SearchText = searchText;

        long tick = GetTick();
        int timeRemain = 200 - (int)(tick - _lastSearchTime);
        if (timeRemain <= 0)
        {
            _lastSearchTime = tick;
            ScheduleSearch();
        }
        else
        {
            CoroutineUtils.Run(async () =>
            {
                await Task.Delay(timeRemain);
                _lastSearchTime = GetTick();
                ScheduleSearch();
            });
        }
    }

    private void ScheduleSearch()
    {
        if (string.IsNullOrEmpty(_searchEngine.SearchText))
        {
            _sharedDispatcher.Submit(() =>
            {
                List<ComicModel> members = _members;
                List<ComicItemViewModel> items = BuildItems(members);
                CoroutineUtils.RunInMainThread(() => ApplyResults(members, items));
            });
            return;
        }

        _searchEngine.Update();
    }

    private void OnSearchResult(IReadOnlyList<ComicModel> comics)
    {
        HashSet<long> memberIds = _memberIds;
        List<ComicModel> filtered = [.. comics.Where(x => memberIds.Contains(x.Id))];
        filtered.Sort(CompareByTitle);

        List<ComicItemViewModel> items = BuildItems(filtered);
        CoroutineUtils.RunInMainThread(() => ApplyResults(filtered, items));
    }

    //
    // Actions
    //

    public void SetRating(double value)
    {
        ComicModel? collection = _collection;
        if (collection is null)
        {
            return;
        }

        if (value < 1.0)
        {
            if (collection.Rating < 0)
            {
                return;
            }

            CoroutineUtils.Run(() => collection.SetRating(-1));
            return;
        }

        int rating = (int)Math.Round(value * 20.0, MidpointRounding.AwayFromZero);
        if (rating == collection.Rating)
        {
            return;
        }

        CoroutineUtils.Run(() => collection.SetRating(rating));
    }

    public void ToggleFavorite()
    {
        long id = _collectionId;
        string title = Name;
        bool isFavorite = IsFavorite;
        _sharedDispatcher.Submit(() =>
        {
            if (isFavorite)
            {
                FavoriteModel.Instance.RemoveWithId(id, true);
            }
            else
            {
                FavoriteModel.Instance.Add(id, title, true);
            }
        });
    }

    public void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        ViewType = viewType;
    }

    //
    // Loading
    //

    private async Task ReloadNoLock()
    {
        Logger.I(TAG, "ReloadNoLock");

        long collectionId = _collectionId;
        ComicModel? collection = await ComicModel.FromId(collectionId);
        if (collection is null || !collection.IsCollection || collection.Id != collectionId)
        {
            _collection = null;
            _members = [];
            _memberIds = [];
            await MainThreadUtils.RunInMainThread(ApplyEmptyState);
            return;
        }

        IReadOnlyList<long> memberIds = await CollectionModel.GetComicIds(collection);
        List<ComicModel> members = await ComicModel.BatchFromId(memberIds);
        members.Sort(CompareByTitle);
        List<ComicItemViewModel> items = BuildItems(members);
        bool isFavorite = FavoriteModel.Instance.FromId(collectionId) != null;

        _collection = collection;
        _members = members;
        _memberIds = [.. members.Select(x => x.Id)];

        await MainThreadUtils.RunInMainThread(() =>
        {
            ApplyInfo(isFavorite);

            if (string.IsNullOrEmpty(_searchEngine.SearchText))
            {
                ApplyResults(members, items);
            }
            else
            {
                // The search is applied again by OnSearchResult
                _searchEngine.Update();
            }
        });
    }

    private void ApplyEmptyState()
    {
        _comics = [];
        Name = string.Empty;
        Description = string.Empty;
        Rating = -1.0;
        CoverImageUri = null;
        BackgroundImageUri = null;
        IsEditable = false;
        IsFavorite = false;
        ItemCount = 0;
        ComicTags.Clear();
        TitleLiveData.Emit(StringResourceProvider.Instance.Collection);
        ResultsLiveData.Emit([]);
    }

    private void ApplyInfo(bool isFavorite)
    {
        ComicModel collection = _collection!;
        Name = collection.Title;
        Description = collection.Description;
        int rating = collection.Rating;
        Rating = rating >= 0 ? rating * 0.05F : -1.0;
        CoverImageUri = ComicExt.GetCoverImageUri(collection);
        BackgroundImageUri = collection.GetExt(ComicExt.BACKGROUND_IMAGE);
        IsEditable = collection.IsEditable;
        IsFavorite = isFavorite;
        TagCollectionViewModel.Update(ComicTags, collection, _actionHandler);
        TitleLiveData.Emit(Name.Length > 0 ? Name : StringResourceProvider.Instance.Collection);
    }

    private void ApplyResults(List<ComicModel> comics, List<ComicItemViewModel> items)
    {
        _comics = comics;
        ItemCount = comics.Count;
        ResultsLiveData.Emit(items);
    }

    private List<ComicItemViewModel> BuildItems(List<ComicModel> comics)
    {
        PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComics(comics);
        List<ComicItemViewModel> items = [];
        foreach (ComicModel comic in comics)
        {
            var item = new ComicItemViewModel(comic)
            {
                OnClick = model =>
                {
                    if (ComicSelection.IsSelectMode)
                    {
                        return;
                    }

                    Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
                    OpenComicHelper.OpenComic(_actionHandler, route);
                },
                OnRequestContextFlyoutAsync = model =>
                {
                    IEnumerable<ComicModel>? selectedComics = ComicSelection.IsSelectMode ? ComicSelection.GetSelectedComics() : null;
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler,
                        comic,
                        playlist: playlist,
                        selectedItems: selectedComics,
                        canSelect: true);
                },
            };
            item.UpdateProgress(true);
            items.Add(item);
        }

        return items;
    }

    //
    // Utilities
    //

    private static int CompareByTitle(ComicModel a, ComicModel b)
    {
        int result = string.Compare(a.Title, b.Title, ignoreCase: true);
        return result != 0 ? result : a.Id.CompareTo(b.Id);
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }
};
