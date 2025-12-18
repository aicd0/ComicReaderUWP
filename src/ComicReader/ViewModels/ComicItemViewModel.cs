// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.ViewModels;

internal partial class ComicItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    //
    // properties
    //

    public ComicModel Comic { get; }

    private string _title;
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    private string _pageCount;
    public string PageCount
    {
        get => _pageCount;
        set
        {
            if (_pageCount != value)
            {
                _pageCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageCountAndProgress)));
            }
        }
    }

    private string _rating = string.Empty;
    public string Rating
    {
        get => _rating;
        set
        {
            if (_rating != value)
            {
                _rating = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rating)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRatingVisible)));
            }
        }
    }

    private string _progress = string.Empty;
    public string Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageCountAndProgress)));
            }
        }
    }

    public string PageCountAndProgress
    {
        get
        {
            string pageCount = PageCount;
            string progress = Progress;
            if (string.IsNullOrEmpty(pageCount))
            {
                return progress;
            }
            else if (string.IsNullOrEmpty(progress))
            {
                return pageCount;
            }
            else
            {
                return $"{pageCount}  ·  {progress}";
            }
        }
    }

    private bool _isFavorite = false;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            }
        }
    }

    private bool _isHidden = false;
    public bool IsHide
    {
        get => _isHidden;
        set
        {
            if (_isHidden != value)
            {
                _isHidden = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHide)));
            }
        }
    }

    private readonly ReaderImageViewModel _image = new();
    public ReaderImageViewModel Image => _image;

    public bool IsRatingVisible => !string.IsNullOrEmpty(Rating);
    public bool IsRead => Comic.CompletionState == ComicCompletionStatusEnum.Completed;
    public bool IsReading => Comic.CompletionState == ComicCompletionStatusEnum.Started;
    public bool IsUnread => Comic.CompletionState == ComicCompletionStatusEnum.NotStarted;

    public Action? OnClick { get; set; }
    public Func<Task<List<BaseMenuFlyoutItemModel>>>? OnRequestContextFlyoutAsync { get; set; }

    //
    // Constructors
    //

    public ComicItemViewModel(ComicModel comic)
    {
        Comic = comic;
        _title = Comic.Title;
        int rating = comic.Rating;
        _rating = rating >= 0 ? Math.Round(rating * 0.05F, 1, MidpointRounding.AwayFromZero).ToString("0.#") : string.Empty;
        _isFavorite = FavoriteModel.Instance.FromId(comic.Id) != null;
        _isHidden = comic.Hidden;
        _pageCount = comic.PageCount > 0 ? $"{comic.PageCount}P" : string.Empty;
    }

    //
    // Utilities
    //

    public ComicItemViewModel Clone()
    {
        var model = new ComicItemViewModel(Comic)
        {
            Progress = Progress,
            OnClick = OnClick,
            OnRequestContextFlyoutAsync = OnRequestContextFlyoutAsync,
        };

        model._image.Image = _image.Image;
        model._image.ImageRequested = _image.ImageRequested;
        return model;
    }

    public void Update(ComicItemViewModel item)
    {
        Title = item.Title;
        Rating = item.Rating;
        IsFavorite = item.IsFavorite;
        IsHide = item.IsHide;
        PageCount = item.PageCount;

        Progress = item.Progress;
        OnClick = item.OnClick;
        OnRequestContextFlyoutAsync = item.OnRequestContextFlyoutAsync;
        _image.Image = item._image.Image;
        _image.ImageRequested = item._image.ImageRequested;
    }

    public void UpdateProgress(bool compat)
    {
        if (Comic.CompletionState == ComicCompletionStatusEnum.NotStarted)
        {
            Progress = StringResourceProvider.Instance.CompletionStatusUnread;
        }
        else if (Comic.CompletionState == ComicCompletionStatusEnum.Completed)
        {
            Progress = StringResourceProvider.Instance.CompletionStatusFinished;
        }
        else
        {
            if (compat)
            {
                Progress = Comic.Progress.ToString() + "%";
            }
            else
            {
                Progress = StringResourceProvider.Instance.FinishPercentage
                    .Replace("$percentage", Comic.Progress.ToString());
            }
        }
    }

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
