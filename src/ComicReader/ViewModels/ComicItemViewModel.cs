// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;

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

    private string _detail = string.Empty;
    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail != value)
            {
                _detail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
            }
        }
    }

    private int _rating = -1;
    public int Rating
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

    private ComicCompletionStatusEnum _completionState = ComicCompletionStatusEnum.NotStarted;
    public ComicCompletionStatusEnum CompletionState
    {
        get => _completionState;
        set
        {
            if (_completionState != value)
            {
                _completionState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompletionState)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRead)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnread)));
            }
        }
    }

    private readonly ReaderImageViewModel _image = new();
    public ReaderImageViewModel Image => _image;

    public bool IsRatingVisible => Rating != -1;
    public bool IsRead => CompletionState == ComicCompletionStatusEnum.Completed;
    public bool IsReading => CompletionState == ComicCompletionStatusEnum.Started;
    public bool IsUnread => CompletionState == ComicCompletionStatusEnum.NotStarted;

    //
    // Constructors
    //

    public ComicItemViewModel(ComicModel comic)
    {
        Comic = comic;
        _title = Comic.Title;
        _rating = comic.Rating;
        _isFavorite = FavoriteModel.Instance.FromId(comic.Id) != null;
        _isHidden = comic.Hidden;
        _completionState = comic.CompletionState;
    }

    //
    // Utilities
    //

    public ComicItemViewModel Clone()
    {
        var model = new ComicItemViewModel(Comic)
        {
            Detail = Detail,
            Progress = Progress
        };

        model._image.Image = _image.Image;
        model._image.ImageRequested = _image.ImageRequested;
        return model;
    }

    public void Update(ComicItemViewModel item)
    {
        Title = item.Title;
        Detail = item.Detail;
        Rating = item.Rating;
        Progress = item.Progress;
        IsFavorite = item.IsFavorite;
        IsHide = item.IsHide;
        CompletionState = item.CompletionState;
        _image.Image = item._image.Image;
        _image.ImageRequested = item._image.ImageRequested;
    }

    public void UpdateProgress(bool compat)
    {
        if (Comic.CompletionState == ComicCompletionStatusEnum.NotStarted)
        {
            Progress = StringResourceProvider.Instance.Unread;
        }
        else if (Comic.CompletionState == ComicCompletionStatusEnum.Completed)
        {
            Progress = StringResourceProvider.Instance.Finished;
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
};
