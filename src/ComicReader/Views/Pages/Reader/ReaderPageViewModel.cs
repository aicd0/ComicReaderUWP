// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.Expression;
using ComicReader.Common.Imaging;
using ComicReader.Common.Localization;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.ViewModels;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Xaml;

using static ComicReader.Views.Pages.Reader.ReaderPage;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(ReaderPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private ComicModel? _comic;
    private ComicModel? _pendingComic;
    private bool _isLoading = false;
    private IComicConnection? _comicConnection;
    private int _pageIndex = -1;
    private bool? _isFavorite = null;

    private readonly ITaskDispatcher _loadPreviewDispatcher = TaskDispatcher.Factory.NewQueue("ReaderLoadPreview");

    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();
    public readonly MutableLiveData<ReaderStatusInfo> ReaderStatusLiveData = new(new(ReaderStatusEnum.Loading));
    public readonly MutableLiveData<ReaderSettingDataModel> ReaderSettingLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<string> ComicDescriptionLiveData = new();
    public readonly MutableLiveData<bool> IsFavoriteLiveData = new();
    public readonly MutableLiveData<ComicCompletionStatusEnum> CompletionStateLiveData = new();
    public readonly MutableLiveData<ReaderLoadingInfo> ReaderLoadingInfoLiveData = new();

    private string _comicTitle1 = "";
    public string ComicTitle1
    {
        get => _comicTitle1;
        set
        {
            _comicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle1)));
        }
    }

    private string _comicTitle2 = "";
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

    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;

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

    private bool _isComicTagsVisible = false;
    public bool IsComicTagsVisible
    {
        get => _isComicTagsVisible;
        set
        {
            _isComicTagsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTagsVisible)));
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

    private string _imageDescription = string.Empty;
    public string ImageDescription
    {
        get => _imageDescription;
        set
        {
            _imageDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageDescription)));
        }
    }

    private bool _isFullscreen = false;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFullscreen)));
        }
    }

    private string _readerCommonStatus = "";
    public string ReaderCommonStatus
    {
        get => _readerCommonStatus;
        set
        {
            _readerCommonStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReaderCommonStatus)));
        }
    }

    public ComicModel? Comic => _comic;
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];
    public bool IsFavorite => _isFavorite ?? false;

    public ReaderPageViewModel() { }

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void CloseComicConnection()
    {
        _comicConnection?.Dispose();
        _comicConnection = null;
    }

    public void SetIsFavorite(bool isFavorite, bool writeDatabase)
    {
        if (_isFavorite == isFavorite)
        {
            return;
        }

        _isFavorite = isFavorite;
        IsFavoriteLiveData.Emit(isFavorite);

        ComicModel? comic = _comic;
        if (writeDatabase && comic != null && !comic.IsExternal)
        {
            if (isFavorite)
            {
                FavoriteModel.Instance.Add(comic.Id, comic.Title1, true);
            }
            else
            {
                FavoriteModel.Instance.RemoveWithId(comic.Id, true);
            }
        }
    }

    public void SetCompletionState(ComicCompletionStatusEnum completionState)
    {
        ComicModel? comic = _comic;
        if (comic is null)
        {
            return;
        }

        if (comic.CompletionState != completionState && !comic.IsExternal)
        {
            switch (completionState)
            {
                case ComicCompletionStatusEnum.NotStarted:
                    _ = comic.SetCompletionStateToNotStarted();
                    break;
                case ComicCompletionStatusEnum.Started:
                    _ = comic.SetCompletionStateToStarted();
                    break;
                case ComicCompletionStatusEnum.Completed:
                    _ = comic.SetCompletionStateToCompleted();
                    break;
                default:
                    break;
            }
        }

        CompletionStateLiveData.Emit(comic.CompletionState);
    }

    public void SetPageIndex(int pageIndex)
    {
        if (pageIndex < 0)
        {
            pageIndex = -1;
        }

        if (pageIndex == _pageIndex)
        {
            return;
        }

        _pageIndex = pageIndex;
        UpdateImageDescription();
    }

    public async Task LoadComic(ComicModel comic)
    {
        if (_isLoading)
        {
            _pendingComic = comic;
            return;
        }

        _isLoading = true;
        try
        {
            ComicModel? loadingComic = comic;
            while (loadingComic != null)
            {
                await LoadComicInternal(loadingComic);
                loadingComic = _pendingComic;
                _pendingComic = null;
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ReloadComicInfo()
    {
        LoadComicInfo();
    }

    public void ReloadReaderSettings()
    {
        LoadReaderSettings();
    }

    public void AddNewTags(string command)
    {
        ComicModel? comic = _comic;
        if (comic is null || string.IsNullOrWhiteSpace(command))
        {
            return;
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

        comic.SetTags(tags);
    }

    private async Task LoadComicInternal(ComicModel comic)
    {
        if (comic == _comic)
        {
            return;
        }

        // Close previous comic
        CloseComicConnection();
        _comic = null;
        PreviewDataSource.Clear();

        // Load new comic
        if (comic == null)
        {
            ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error));
            return;
        }

        _comic = comic;

        if (!comic.IsExternal)
        {
            await comic.SetCompletionStateToAtLeastStarted();
            if (AppModel.SaveBrowsingHistory)
            {
                HistoryModel.Instance.Add(comic.Id, comic.Title1, true);
            }
        }

        LoadReaderSettings();
        LoadComicInfo();

        if (!comic.IsExternal && !await comic.ReloadImageFiles())
        {
            Logger.I(TAG, "Failed to load images of '" + comic.Location + "'. ");
            ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error));
            return;
        }

        IComicConnection? connection = await comic.OpenComicAsync();
        if (connection is null)
        {
            ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error));
            return;
        }

        _comicConnection = connection;
        ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Loading));

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }

        if (images.Count == 0)
        {
            ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error));
            return;
        }

        bool restorePosition = AppSettingsModel.Instance.GetModel().RestoreLastReadingPosition && !comic.IsExternal;
        ReaderLoadingInfoLiveData.Emit(new(images, restorePosition ? comic.LastPosition : 0.0));

        // Load preview images
        double previewWidth = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double previewHeight = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(comic, connection, i),
                    Width = previewWidth,
                    Height = previewHeight,
                    Dispatcher = _loadPreviewDispatcher,
                    DebugDescription = i.ToString()
                },
                Page = i + 1,
            });
        }
    }

    private void LoadReaderSettings()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        AppSettingsModel.ReaderSettingModel readerSettings = AppSettingsModel.Instance.GetModel().DefaultReaderSetting;
        var readerSettingModel = ReaderSettingDataModel.From(readerSettings, comic);
        ReaderSettingLiveData.Emit(readerSettingModel);
    }

    private void LoadComicInfo()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

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

        LoadComicTag();

        bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
        SetIsFavorite(isFavorite, false);

        SetCompletionState(comic.CompletionState);

        if (!comic.IsExternal)
        {
            Rating = comic.Rating;
        }
    }

    private void LoadComicTag()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        var newCollection = new ObservableCollection<TagCollectionViewModel>();

        for (int i = 0; i < comic.Tags.Count; ++i)
        {
            ComicData.TagData tags = comic.Tags[i];
            var tagCollectionModel = new TagCollectionViewModel(tags.Name);
            foreach (string tag in tags.Tags)
            {
                TagViewModel tagModel = new()
                {
                    Tag = tag,
                    OnClicked = () =>
                    {
                        string expression = $"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(tags.Name)}\"=\"{ExpressionUtils.EscapeString(tag)}\"";
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                            .WithParam(RouterConstants.ARG_KEYWORD, $"exp:\"{ExpressionUtils.EscapeString(expression)}\"");
                        ActionModel actionModel = ActionModel.Builder.Create(OpenInNewTabProvider.NAME)
                            .AddParameter(OpenInNewTabProvider.PARAM_URL, route.Url)
                            .Build();
                        _actionHandler.Handle(actionModel);
                    },
                    OnRequestContextFlyoutAsync = () =>
                    {
                        return CreateTagContextMenuItems(tags.Name, tag);
                    },
                };

                tagCollectionModel.Tags.Add(tagModel);
            }

            newCollection.Add(tagCollectionModel);
        }

        DiffUtils.UpdateCollection(ComicTags, newCollection, (x, y) => x.Name == y.Name, (x, y) =>
        {
            DiffUtils.UpdateCollection(x.Tags, y.Tags, (a, b) => a.Tag == b.Tag, (a, b) =>
            {
                a.OnRequestContextFlyoutAsync = b.OnRequestContextFlyoutAsync;
                a.OnClicked = b.OnClicked;
            });
        });

        IsComicTagsVisible = newCollection.Count > 0;
    }

    private async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagContextMenuItems(string tagCategory, string tag)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(tagCategory, tag, _actionHandler),
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            OnClick = () =>
            {
                EditTagLiveData.Emit(new(tagCategory, tag));
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            OnClick = () =>
            {
                ComicModel? comic = _comic;
                if (comic == null)
                {
                    return;
                }

                Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
                if (tags.TryGetValue(tagCategory, out HashSet<string>? tagSet))
                {
                    if (tagSet.Remove(tag))
                    {
                        comic.SetTags(tags);
                    }
                }
            },
        });

        return items;
    }

    private void UpdateImageDescription()
    {
        ComicModel? comic = _comic;
        IComicConnection? comicConnection = _comicConnection;
        int pageIndex = _pageIndex;
        if (comic is null || pageIndex < 0 || comicConnection is null)
        {
            ImageDescription = string.Empty;
            return;
        }

        int imageCount = comicConnection.GetImageCount();
        if (pageIndex >= imageCount)
        {
            ImageDescription = string.Empty;
            return;
        }

        string imageName = comicConnection.GetImageName(pageIndex);
        var imageSource = new ComicImageSource(comic, comicConnection, pageIndex);
        TaskDispatcher.DefaultQueue.Submit("LoadImageMeta", () =>
        {
            ImageCacheManager.ImageMeta? imageMeta = ImageCacheManager.GetImageMeta(imageSource);
            StringBuilder imageDescriptionSb = new();

            if (!string.IsNullOrEmpty(imageName))
            {
                imageDescriptionSb.Append(imageName);
            }

            if (imageMeta is not null)
            {
                if (imageDescriptionSb.Length > 0)
                {
                    imageDescriptionSb.Append('\n');
                }

                imageDescriptionSb.Append(imageMeta.Format);
                imageDescriptionSb.Append(' ').Append(imageMeta.Width).Append(" x ").Append(imageMeta.Height);
                imageDescriptionSb.Append(' ').Append(FormatBytes(imageMeta.Size));

                if (imageMeta.DpiX > 0 && imageMeta.DpiY > 0)
                {
                    imageDescriptionSb.Append(' ').Append(FormatDpi(imageMeta.DpiX, imageMeta.DpiY));
                }

                imageDescriptionSb.Append(' ').Append(imageMeta.BitsPerPixel).Append(" bits");
            }

            string imageDescription = imageDescriptionSb.ToString();
            MainThreadUtils.RunInMainThread(() =>
            {
                ImageDescription = imageDescription;
            });
        });
    }

    private static string FormatDpi(int dpiX, int dpiY)
    {
        if (dpiX == dpiY)
        {
            return $"{dpiX} dpi";
        }
        else
        {
            return $"{dpiX} x {dpiY} dpi";
        }
    }

    private static string FormatBytes(long byteCount)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        int unitIndex = (int)Math.Floor(Math.Log(byteCount, 1024));
        double adjustedSize = byteCount / Math.Pow(1024, unitIndex);
        return $"{adjustedSize:0.#} {units[unitIndex]}";
    }

    //
    // Types
    //

    public class ReaderLoadingInfo(IEnumerable<IImageSource> images, double initialPage)
    {
        public readonly IEnumerable<IImageSource> Images = images;
        public readonly double InitialPage = initialPage;
    }
}
