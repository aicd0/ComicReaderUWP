// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Dialogs.EditReaderSettingPreset;

internal partial class EditReaderSettingPresetDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

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

    private bool _setAsDefault = false;
    public bool SetAsDefault
    {
        get => _setAsDefault;
        set
        {
            _setAsDefault = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SetAsDefault)));
        }
    }

    private bool _saveEnabled = false;
    public bool SaveEnabled
    {
        get => _saveEnabled;
        set
        {
            _saveEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveEnabled)));
        }
    }

    private bool _saveAsNewEnabled = false;
    public bool SaveAsNewEnabled
    {
        get => _saveAsNewEnabled;
        set
        {
            _saveAsNewEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveAsNewEnabled)));
        }
    }

    private bool _deleteEnabled = false;
    public bool DeleteEnabled
    {
        get => _deleteEnabled;
        set
        {
            _deleteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteEnabled)));
        }
    }

    private ComicModel? _comic = null;
    private ReaderSettingsModel _presetModel = ReaderSettingsModel.FromDefault();

    public void Initialize(ComicModel comic)
    {
        _comic = comic;
        _presetModel = ReaderSettingsModel.LoadFromComic(comic);

        Title = StringResourceProvider.Instance.EditPreset;
        Name = _presetModel.PresetName;
        SetAsDefault = _presetModel.PresetKey == AppSettingsModel.DefaultReaderSettingPresetKey;
        UpdateUI();
    }

    public void Delete()
    {
        if (_presetModel.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM)
        {
            return;
        }

        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.ReaderSettingPresets;
        presets.Remove(_presetModel.PresetKey);
        AppSettingsModel.ReaderSettingPresets = presets;

        if (_comic is not null)
        {
            _comic.SetExt(ComicExt.READER_SETTING_PRESET_KEY, null);
            _comic.SetExt(ComicExt.CUSTOM_READER_SETTINGS, null);
        }
    }

    public void Save()
    {
        string name = _name.Trim();
        if (string.IsNullOrEmpty(name) || _presetModel.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM)
        {
            return;
        }

        _presetModel.PresetName = name;
        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.ReaderSettingPresets;
        presets[_presetModel.PresetKey] = _presetModel;
        AppSettingsModel.ReaderSettingPresets = presets;

        if (_setAsDefault)
        {
            AppSettingsModel.DefaultReaderSettingPresetKey = _presetModel.PresetKey;
        }

        if (_comic is not null)
        {
            _presetModel.SaveToComic(_comic);
        }
    }

    public void SaveAsNew()
    {
        string name = _name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        _presetModel.PresetKey = Guid.NewGuid().ToString();
        _presetModel.PresetName = name;
        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.ReaderSettingPresets;
        presets[_presetModel.PresetKey] = _presetModel;
        AppSettingsModel.ReaderSettingPresets = presets;

        if (_setAsDefault)
        {
            AppSettingsModel.DefaultReaderSettingPresetKey = _presetModel.PresetKey;
        }

        if (_comic is not null)
        {
            _presetModel.SaveToComic(_comic);
        }
    }

    public void UpdateName(string name)
    {
        _name = name;
        UpdateUI();
    }

    public void UpdateSetAsDefault(bool isDefault)
    {
        _setAsDefault = isDefault;
    }

    private void UpdateUI()
    {
        string name = _name.Trim();
        bool isCustomPreset = _presetModel.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM;
        bool isNameValid = !string.IsNullOrEmpty(name);
        bool isNameExisting = false;
        foreach (ReaderSettingsModel preset in AppSettingsModel.ReaderSettingPresets.Values)
        {
            if (preset.PresetName == name)
            {
                isNameExisting = true;
                break;
            }
        }

        SaveEnabled = !isCustomPreset && isNameValid;
        SaveAsNewEnabled = isNameValid && !isNameExisting;
        DeleteEnabled = !isCustomPreset;
    }
}
