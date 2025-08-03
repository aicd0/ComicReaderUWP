// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.Lifecycle;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<string> TagClickLiveData = new();

    public readonly TagItemHandler _tagItemHandler;

    public ReaderPageViewModel()
    {
        _tagItemHandler = new(this);
    }

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

    private ObservableCollection<TagCollectionViewModel> _comicTags = [];
    public ObservableCollection<TagCollectionViewModel> ComicTags
    {
        get => _comicTags;
        set
        {
            _comicTags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTags)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTagsVisible)));
        }
    }

    public bool IsComicTagsVisible => ComicTags != null && ComicTags.Count > 0;

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

    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];

    public class TagItemHandler(ReaderPageViewModel viewModel) : TagViewModel.IItemHandler
    {
        public void OnClicked(object sender, RoutedEventArgs e)
        {
            var ctx = (TagViewModel)((Button)sender).DataContext;
            viewModel.TagClickLiveData.Emit(ctx.Tag);
        }
    }
}
