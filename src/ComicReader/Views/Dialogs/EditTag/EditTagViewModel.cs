// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common.Utils;
using ComicReader.Data.Models;

namespace ComicReader.Views.Dialogs.EditTag;

internal partial class EditTagDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = string.Empty;
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

    private string _tagCategoryName = string.Empty;
    public string TagCategoryName
    {
        get => _tagCategoryName;
        set
        {
            if (_tagCategoryName != value)
            {
                _tagCategoryName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagCategoryName)));
            }
        }
    }

    private string _tagName = string.Empty;
    public string TagName
    {
        get => _tagName;
        set
        {
            if (_tagName != value)
            {
                _tagName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagName)));
            }
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            }
        }
    }

    private bool _saveEnabled = false;
    public bool SaveEnabled
    {
        get => _saveEnabled;
        set
        {
            if (_saveEnabled != value)
            {
                _saveEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveEnabled)));
            }
        }
    }

    private bool _overwriteWarning = false;
    public bool OverwriteWarning
    {
        get => _overwriteWarning;
        set
        {
            if (_overwriteWarning != value)
            {
                _overwriteWarning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverwriteWarning)));
            }
        }
    }

    private string _oldTagCategoryName = string.Empty;
    private string _oldTagName = string.Empty;
    private bool _isNameValid = false;
    private TagInfoModel? _tagInfoModel;

    private bool IsSameTag =>
        _tagCategoryName == _oldTagCategoryName && _tagName == _oldTagName;

    public void Initialize(string tagCategory, string tag)
    {
        _oldTagCategoryName = tagCategory;
        _oldTagName = tag;
        Title = tag;
        TagCategoryName = tagCategory;
        TagName = tag;
        UpdateUIStates();

        CoroutineUtils.Start(async () =>
        {
            TagInfoModel tagInfoModel = await TagInfoModel.GetOrCreate(tagCategory, tag);
            _tagInfoModel = tagInfoModel;
            Description = tagInfoModel.GetExt(TagInfoExt.DESCRIPTION) ?? string.Empty;
        });
    }

    public void UpdateTagCategoryName(string name)
    {
        name = name.Trim();
        if (name == _tagCategoryName)
        {
            return;
        }

        _tagCategoryName = name;
        UpdateUIStates();
    }

    public void UpdateTagName(string name)
    {
        name = name.Trim();
        if (name == _tagName)
        {
            return;
        }

        _tagName = name;
        UpdateUIStates();
    }

    public void UpdateDescription(string description)
    {
        _description = description;
    }

    public void Save()
    {
        if (!_isNameValid)
        {
            return;
        }

        CoroutineUtils.Start(async () =>
        {
            if (!IsSameTag)
            {
                await TagInfoModel.RenameTag(_oldTagCategoryName, _oldTagName, _tagCategoryName, _tagName);
            }

            if (_tagInfoModel != null)
            {
                _tagInfoModel.SetExt(TagInfoExt.DESCRIPTION, _description);
                _tagInfoModel.FlushExt();
            }
        });
    }

    private void UpdateUIStates()
    {
        _isNameValid = !string.IsNullOrEmpty(_tagCategoryName) && !string.IsNullOrEmpty(_tagName);
        SaveEnabled = _isNameValid;

        CoroutineUtils.Start(async () =>
        {
            OverwriteWarning = !IsSameTag && await TagInfoModel.Get(_tagCategoryName, _tagName) != null;
        });
    }
}
