// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;

using ComicReader.Common.Localization;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Views.Pages.Main;

namespace ComicReader.Views.Dialogs.EditReaderSettingPreset;

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
    private ReaderSettingDataModel _presetModel = new();

    public void Initialize(ComicModel comic)
    {
        _comic = comic;
        _presetModel = ReaderSettingDataModel.FromComic(comic);

        Title = StringResourceProvider.Instance.EditPreset;
        Name = _presetModel.PresetName;
        SetAsDefault = _presetModel.PresetKey == AppSettingsModel.Instance.GetModel().DefaultReaderSettingPresetKey;
        UpdateUI();
    }

    public void Delete()
    {
        if (_presetModel.PresetKey == ReaderSettingDataModel.PRESET_KEY_CUSTOM)
        {
            return;
        }

        AppSettingsModel.ExternalModel settingModel = AppSettingsModel.Instance.GetModel();
        settingModel.ReaderSettingPresets.Remove(_presetModel.PresetKey);
        AppSettingsModel.Instance.UpdateModel(settingModel);
    }

    public void Save()
    {
        string name = _name.Trim();
        if (string.IsNullOrEmpty(name) || _presetModel.PresetKey == ReaderSettingDataModel.PRESET_KEY_CUSTOM)
        {
            return;
        }

        _presetModel.PresetName = name;
        AppSettingsModel.ExternalModel settingModel = AppSettingsModel.Instance.GetModel();
        settingModel.ReaderSettingPresets[_presetModel.PresetKey] = _presetModel.ToSettingModel();

        if (_setAsDefault)
        {
            settingModel.DefaultReaderSettingPresetKey = _presetModel.PresetKey;
        }

        AppSettingsModel.Instance.UpdateModel(settingModel);
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
        AppSettingsModel.ExternalModel settingModel = AppSettingsModel.Instance.GetModel();
        settingModel.ReaderSettingPresets[_presetModel.PresetKey] = _presetModel.ToSettingModel();

        if (_setAsDefault)
        {
            settingModel.DefaultReaderSettingPresetKey = _presetModel.PresetKey;
        }

        AppSettingsModel.Instance.UpdateModel(settingModel);

        if (_comic is not null)
        {
            _presetModel.ToComic(_comic);
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
        bool isCustomPreset = _presetModel.PresetKey == ReaderSettingDataModel.PRESET_KEY_CUSTOM;
        bool isNameValid = !string.IsNullOrEmpty(name);
        bool isNameExisting = false;
        foreach (AppSettingsModel.ReaderSettingModel preset in AppSettingsModel.Instance.GetModel().ReaderSettingPresets.Values)
        {
            if (preset.PresetName == name)
            {
                isNameExisting = true;
                break;
            }
        }

        SaveEnabled = (!isCustomPreset && isNameValid) || (isCustomPreset && string.IsNullOrEmpty(_name));
        SaveAsNewEnabled = isNameValid && !isNameExisting;
        DeleteEnabled = !isCustomPreset;
    }
}
