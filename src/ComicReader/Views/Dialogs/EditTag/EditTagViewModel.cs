// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.Utils;
using ComicReader.Data.Models.TagInfo;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.ViewModels;

namespace ComicReader.Views.Dialogs.EditTag;

internal partial class EditTagDialogViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(EditTagDialogViewModel);

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

    public ObservableCollection<LinkItemViewModel> Links { get; } = [];

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
        UpdateSaveButtonStates();

        CoroutineUtils.Start(async () =>
        {
            TagInfoModel tagInfoModel = await TagInfoModel.GetOrCreate(tagCategory, tag);
            _tagInfoModel = tagInfoModel;

            Description = tagInfoModel.GetExt(TagInfoExt.DESCRIPTION) ?? string.Empty;
            UpdateLinks(tagInfoModel.GetExt(TagInfoExt.LINKS) ?? string.Empty);
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
        UpdateSaveButtonStates();
    }

    public void UpdateTagName(string name)
    {
        name = name.Trim();
        if (name == _tagName)
        {
            return;
        }

        _tagName = name;
        UpdateSaveButtonStates();
    }

    public void UpdateDescription(string description)
    {
        _description = description;
    }

    public void AddLink()
    {
        if (Links.Count == 0)
        {
            Logger.F(TAG, "Cannot add link, collection cannot be empty.");
            return;
        }

        Links.Insert(Links.Count - 1, new()
        {
            IsPlaceholder = false,
            Name = string.Empty,
            Link = string.Empty,
            Global = false,
        });
    }

    public void RemoveLink(LinkItemViewModel item)
    {
        Links.Remove(item);
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
                await TagInfoModel.Rename(_oldTagCategoryName, _oldTagName, _tagCategoryName, _tagName);
            }

            if (_tagInfoModel != null)
            {
                _tagInfoModel.SetExt(TagInfoExt.DESCRIPTION, _description);
                _tagInfoModel.SetExt(TagInfoExt.LINKS, GetSerializedLinksAndSaveGlobalLinks());
                _tagInfoModel.FlushExt();
            }
        });
    }

    private void UpdateSaveButtonStates()
    {
        _isNameValid = !string.IsNullOrEmpty(_tagCategoryName) && !string.IsNullOrEmpty(_tagName);
        SaveEnabled = _isNameValid;

        CoroutineUtils.Start(async () =>
        {
            OverwriteWarning = !IsSameTag && await TagInfoModel.Get(_tagCategoryName, _tagName) != null;
        });
    }

    private void UpdateLinks(string json)
    {
        var model = TagLinkModel.Parse(json);
        foreach (TagLinkModel.LinkModel link in model.Links)
        {
            Links.Add(new()
            {
                IsPlaceholder = false,
                Name = link.Name,
                Link = link.Link,
                Global = link.Global,
            });
        }

        Links.Add(new()
        {
            IsPlaceholder = true,
        });
    }

    private string GetSerializedLinksAndSaveGlobalLinks()
    {
        TagLinkModel model = new();
        foreach (LinkItemViewModel item in Links)
        {
            if (item.IsPlaceholder)
            {
                continue;
            }

            if (string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.Link))
            {
                continue;
            }

            model.Links.Add(new()
            {
                Name = item.Name,
                Link = item.Link,
                Global = item.Global,
            });
        }

        return model.SerializeAndSaveGlobal();
    }
}
