// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.TagInfo;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.SqlHelpers;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Dialogs.EditTagCategory;

internal partial class EditTagCateogoryDialogViewModel : INotifyPropertyChanged
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

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
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

    private string _oldName = string.Empty;
    private bool _isNameValid = false;
    private TagCategoryInfoModel? _tagCategoryInfoModel;

    private bool IsSameCategory =>
        _oldName == _name;

    public void Initialize(string tagCategory)
    {
        _oldName = tagCategory;
        Title = tagCategory;
        Name = tagCategory;
        UpdateSaveButtonStates();

        CoroutineUtils.Start(async () =>
        {
            TagCategoryInfoModel tagCategoryInfoModel = await TagCategoryInfoModel.GetOrCreate(tagCategory);
            _tagCategoryInfoModel = tagCategoryInfoModel;

            UpdateLinks(tagCategoryInfoModel.GetExt(TagCategoryInfoExt.LINKS) ?? string.Empty);
        });
    }

    public void UpdateName(string name)
    {
        name = name.Trim();
        if (name == _name)
        {
            return;
        }

        _name = name;
        UpdateSaveButtonStates();
    }

    public void Save()
    {
        if (!_isNameValid)
        {
            return;
        }

        CoroutineUtils.Start(() => BusyStateManager.WithBusyState(async () =>
        {
            if (!IsSameCategory)
            {
                await TagCategoryInfoModel.Rename(_oldName, _name);
            }

            if (_tagCategoryInfoModel != null)
            {
                _tagCategoryInfoModel.SetExt(TagCategoryInfoExt.LINKS, GetSerializedLinks());
                await _tagCategoryInfoModel.FlushExt();
            }
        }));
    }

    private void UpdateSaveButtonStates()
    {
        _isNameValid = !string.IsNullOrEmpty(_name);
        SaveEnabled = _isNameValid;

        CoroutineUtils.Start(async () =>
        {
            OverwriteWarning = !IsSameCategory && await MayOverwriteExistingEntries(_name);
        });
    }

    private async Task<bool> MayOverwriteExistingEntries(string tagCategory)
    {
        return await TagInfoDatabase.Enqueue("MayOverwriteExistingEntries", () =>
        {
            SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
                .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory);
            command.PutQueryString(TagInfoTable.ColumnName);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                return true;
            }

            return false;
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
            });
        }

        Links.Add(new()
        {
            IsPlaceholder = true,
        });
    }

    private string GetSerializedLinks()
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
            });
        }

        return model.Serialize();
    }
}
