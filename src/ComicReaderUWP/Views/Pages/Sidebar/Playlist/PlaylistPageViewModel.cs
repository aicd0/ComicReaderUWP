// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

namespace ComicReaderUWP.Views.Pages.Sidebar.Playlist;

internal partial class PlaylistPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _noComicsVisible = false;
    public bool NoComicsVisible
    {
        get => _noComicsVisible;
        set
        {
            if (_noComicsVisible != value)
            {
                _noComicsVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoComicsVisible)));
            }
        }
    }

    private int _selectedIndex = -1;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
        }
    }

    public ObservableCollection<PlaylistItemViewModel> DataSource { get; set; } = [];

    public MutableLiveData<PlaylistItemViewModel> ScrollToItemLiveData = new();

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private PlaybackModel _playback = new();

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        UpdatePlaylist();
    }

    public void Destory()
    {
        _playback.PlaylistChanged -= Playback_PlaylistChanged;
        _playback.PlaybackStatusChanged -= Playback_PlaybackStatusChanged;
    }

    public void SetPlayback(PlaybackModel playback)
    {
        _playback.PlaylistChanged -= Playback_PlaylistChanged;
        _playback.PlaybackStatusChanged -= Playback_PlaybackStatusChanged;
        _playback = playback;
        _playback.PlaylistChanged += Playback_PlaylistChanged;
        _playback.PlaybackStatusChanged += Playback_PlaybackStatusChanged;
        UpdatePlaylist();
        UpdatePlaybackStatus();
    }

    public void SetSelectedIndex(int index)
    {
        if (_selectedIndex == index)
        {
            return;
        }

        _selectedIndex = index;
        _playback.Cursor = index;
    }

    public void UpdateComics()
    {
        UpdatePlaylist();
    }

    private void Playback_PlaylistChanged()
    {
        UpdatePlaylist();
    }

    private void Playback_PlaybackStatusChanged(PlaybackModel.StatusChangeReason reason)
    {
        UpdatePlaybackStatus();
    }

    private void UpdatePlaylist()
    {
        List<PlaylistItemViewModel> newPlaylist = [];
        PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddItems(_playback.Items);
        foreach (PlaylistModel.PlaylistItem playlistItem in _playback.Items)
        {
            newPlaylist.Add(new()
            {
                Comic = playlistItem.Comic,
                Title = playlistItem.Comic.Title,
                Progress = GetProgressText(playlistItem.Comic),
                RequestContextMenuItemsAsync = item =>
                {
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler,
                        playlistItem.Comic,
                        playlist: playlist,
                        playback: _playback.ToBuilder());
                },
            });
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            DiffUtils.UpdateCollection(DataSource, newPlaylist, (a, b) => a.Comic == b.Comic, (a, b) =>
            {
                a.Title = b.Title;
                a.Progress = b.Progress;
                a.RequestContextMenuItemsAsync = b.RequestContextMenuItemsAsync;
            });
            NoComicsVisible = DataSource.Count == 0;
        });
    }

    private void UpdatePlaybackStatus()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            int cursor = _playback.Cursor;
            if (cursor >= 0 && cursor < DataSource.Count)
            {
                SelectedIndex = cursor;
                ScrollToItemLiveData.Emit(DataSource[cursor]);
            }
        });
    }

    private static string GetProgressText(ComicModel comic)
    {
        ComicCompletionStatusEnum status = comic.CompletionStatus;

        if (ComicCompletionStatusService.CanTransitToReadingAutomatically(status))
        {
            return string.Empty;
        }

        return status switch
        {
            ComicCompletionStatusEnum.Reading => Math.Clamp(comic.Progress, 0, 100).ToString() + "%",
            _ => ComicCompletionStatusService.EnumToString(status),
        };
    }
}
