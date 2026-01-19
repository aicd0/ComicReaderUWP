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

    private PlaylistModel _playlist = PlaylistModel.CreateEmpty();
    private readonly List<PlaylistModel.PlaylistItem> _items = [];
    private int _cursor = 0;
    private int _randomSeed = Random.Shared.Next();
    private string? _firstId;

    public IReadOnlyList<PlaylistModel.PlaylistItem> Items => _items;
    public bool CanGoNext => _isRepeat || _cursor < _items.Count - 1;
    public bool CanGoPrevious => _isRepeat || _cursor > 0;

    private bool _isRepeat = AppSettingsModel.Instance.PlaybackDefaultRepeat;
    public bool IsRepeat
    {
        get => _isRepeat;
        set
        {
            if (_isRepeat != value)
            {
                _isRepeat = value;
                AppSettingsModel.Instance.PlaybackDefaultRepeat = value;
                DispatchPlaybackStatusChange(StatusChangeReason.Other);
            }
        }
    }

    private bool _isShuffle = AppSettingsModel.Instance.PlaybackDefaultShuffle;
    public bool IsShuffle
    {
        get => _isShuffle;
        set
        {
            if (_isShuffle != value)
            {
                _isShuffle = value;
                AppSettingsModel.Instance.PlaybackDefaultShuffle = value;

                if (value)
                {
                    _randomSeed = Random.Shared.Next();
                    _firstId = CurrentItem?.Id;
                }

                UpdatePlaylist();
            }
        }
    }

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
            ClampCursor();
            DispatchPlaybackStatusChange(StatusChangeReason.Other);
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

        if (!string.IsNullOrEmpty(serializedPlayback) && FromSerializedString(serializedPlayback))
        {
            return;
        }

        UpdatePlaylist();
    }

    public void Next()
    {
        if (_cursor >= Items.Count - 1)
        {
            if (_isRepeat)
            {
                _cursor = 0;
            }
            else
            {
                return;
            }
        }
        else
        {
            _cursor++;
        }

        ClampCursor();
        DispatchPlaybackStatusChange(StatusChangeReason.Next);
    }

    public void Previous(bool fromOverScroll)
    {
        if (_cursor <= 0)
        {
            if (_isRepeat)
            {
                _cursor = Items.Count - 1;
            }
            else
            {
                return;
            }
        }
        else
        {
            _cursor--;
        }

        ClampCursor();
        DispatchPlaybackStatusChange(fromOverScroll ? StatusChangeReason.PreviousByOverScroll : StatusChangeReason.Previous);
    }

    public string ToSerializedString()
    {
        PlaybackJsonModel model = new()
        {
            CurrentId = CurrentItem?.Id,
            IsRepeat = _isRepeat,
            IsShuffle = _isShuffle,
            RandomSeed = _randomSeed,
            FirstId = _firstId,
        };
        return JsonSerializer.Serialize(model);
    }

    private void UpdatePlaylist()
    {
        PlaylistModel.PlaylistItem? currentItem = CurrentItem;
        PopulateItems();
        if (currentItem is not null)
        {
            _cursor = Math.Max(0, _items.FindIndex(x => x.Id == currentItem.Id));
        }
        else
        {
            _cursor = 0;
        }

        ClampCursor();
        DispatchPlaylistChange();
        DispatchPlaybackStatusChange(StatusChangeReason.Other);
    }

    private bool FromSerializedString(string serialized)
    {
        PlaybackJsonModel? model;
        try
        {
            model = JsonSerializer.Deserialize<PlaybackJsonModel>(serialized);
        }
        catch (JsonException ex)
        {
            Logger.E(TAG, ex);
            return false;
        }

        if (model is null)
        {
            return false;
        }

        _isRepeat = model.IsRepeat ?? AppSettingsModel.Instance.PlaybackDefaultRepeat;
        _isShuffle = model.IsShuffle ?? AppSettingsModel.Instance.PlaybackDefaultShuffle;
        _randomSeed = model.RandomSeed ?? Random.Shared.Next();
        _firstId = model.FirstId;
        PopulateItems();

        _cursor = 0;
        string? currentId = model.CurrentId;
        if (!string.IsNullOrEmpty(currentId))
        {
            int cursor = _items.FindIndex(x => x.Id == currentId);
            if (cursor >= 0)
            {
                _cursor = cursor;
            }
        }

        ClampCursor();
        DispatchPlaylistChange();
        DispatchPlaybackStatusChange(StatusChangeReason.Other);
        return true;
    }

    private void PopulateItems()
    {
        _items.Clear();
        _items.AddRange(_playlist.Items);

        if (_isShuffle)
        {
            var rng = new Random(_randomSeed);
            for (int i = _items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_items[i], _items[j]) = (_items[j], _items[i]);
            }

            if (!string.IsNullOrEmpty(_firstId))
            {
                int index = _items.FindIndex(x => x.Id == _firstId);
                if (index >= 0)
                {
                    PlaylistModel.PlaylistItem firstItem = _items[index];
                    _items.RemoveAt(index);
                    _items.Insert(0, firstItem);
                }
            }
        }
    }

    private void ClampCursor()
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
        Other,
        Next,
        Previous,
        PreviousByOverScroll,
    }

    private class PlaybackJsonModel
    {
        [JsonPropertyName("CurrentId")]
        public string? CurrentId { get; set; }

        [JsonPropertyName("FirstId")]
        public string? FirstId { get; set; }

        [JsonPropertyName("IsRepeat")]
        public bool? IsRepeat { get; set; }

        [JsonPropertyName("IsShuffle")]
        public bool? IsShuffle { get; set; }

        [JsonPropertyName("RandomSeed")]
        public int? RandomSeed { get; set; }
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
