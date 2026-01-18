// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReaderUWP.SDK.Common.DebugTools;

namespace ComicReaderUWP.Data.Models.Misc;

internal class PlaybackModel
{
    private const string TAG = nameof(PlaybackModel);

    public delegate void PlaybackStatusChangedEventHandler(StatusChangeReason reason);
    public event PlaybackStatusChangedEventHandler? PlaybackStatusChanged;

    public delegate void PlaylistChangedEventHandler();
    public event PlaylistChangedEventHandler? PlaylistChanged;

    private int _cursor = 0;
    private PlaylistModel _playlist = PlaylistModel.CreateEmpty();
    private readonly List<PlaylistModel.PlaylistItem> _items = [];

    public IReadOnlyList<PlaylistModel.PlaylistItem> Items => _items;
    public bool CanGoNext => _cursor < _items.Count - 1;
    public bool CanGoPrevious => _cursor > 0;

    public int Cursor
    {
        get => _cursor;
        set
        {
            if (_cursor == value || value < 0 || value >= _items.Count)
            {
                return;
            }

            _cursor = value;
            FixCursor();
            DispatchPlaybackStatusChange(StatusChangeReason.SetCursor);
        }
    }

    public PlaylistModel.PlaylistItem? CurrentItem
    {
        get
        {
            if (_cursor < 0 || _cursor >= _items.Count)
            {
                return null;
            }

            return _items[_cursor];
        }
    }

    public void SetPlaylist(PlaylistModel playlist, string? serializedPlayback)
    {
        _playlist = playlist;
        _items.Clear();
        _items.AddRange(playlist.Items);
        _cursor = 0;

        if (!string.IsNullOrEmpty(serializedPlayback))
        {
            FromSerializedString(serializedPlayback);
        }

        FixCursor();
        DispatchPlaylistChange();
        DispatchPlaybackStatusChange(StatusChangeReason.SetCursor);
    }

    public void Next()
    {
        _cursor++;
        FixCursor();
        DispatchPlaybackStatusChange(StatusChangeReason.Next);
    }

    public void Previous(bool fromOverScroll)
    {
        _cursor--;
        FixCursor();
        DispatchPlaybackStatusChange(fromOverScroll ? StatusChangeReason.PreviousByOverScroll : StatusChangeReason.Previous);
    }

    public string ToSerializedString()
    {
        PlaybackJsonModel model = new()
        {
            CurrentId = CurrentItem?.Id,
        };
        return JsonSerializer.Serialize(model);
    }

    private void FromSerializedString(string serialized)
    {
        PlaybackJsonModel? model;
        try
        {
            model = JsonSerializer.Deserialize<PlaybackJsonModel>(serialized);
        }
        catch (JsonException ex)
        {
            Logger.E(TAG, ex);
            return;
        }

        if (model is null)
        {
            return;
        }

        string? currentId = model.CurrentId;
        if (string.IsNullOrEmpty(currentId))
        {
            return;
        }

        int cursor = _items.FindIndex(x => x.Id == currentId);
        if (cursor < 0)
        {
            return;
        }

        _cursor = cursor;
    }

    private void FixCursor()
    {
        _cursor = Math.Max(Math.Min(_cursor, _items.Count - 1), 0);
    }

    private void DispatchPlaybackStatusChange(StatusChangeReason reason)
    {
        PlaybackStatusChanged?.Invoke(reason);
    }

    private void DispatchPlaylistChange()
    {
        PlaylistChanged?.Invoke();
    }

    public enum StatusChangeReason
    {
        SetCursor,
        Next,
        Previous,
        PreviousByOverScroll,
    }

    private class PlaybackJsonModel
    {
        [JsonPropertyName("CurrentId")]
        public string? CurrentId { get; set; }
    }

    public class Builder
    {
        public static Builder Create()
        {
            return new();
        }

        private readonly PlaybackJsonModel _model = new();

        private Builder() { }

        public Builder SetCurrentId(string currentId)
        {
            _model.CurrentId = currentId;
            return this;
        }

        public string ToSerializedString()
        {
            return JsonSerializer.Serialize(_model);
        }
    }
}
