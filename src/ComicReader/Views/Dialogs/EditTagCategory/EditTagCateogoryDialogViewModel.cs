// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common.Utils;
using ComicReader.Data.Models.Tags;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Views.Dialogs.EditTagCategory;

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

    private string _oldName = string.Empty;
    private bool _isNameValid = false;

    private bool IsSameCategory =>
        _oldName == _name;

    public void Initialize(string tagCategory)
    {
        _oldName = tagCategory;
        Title = tagCategory;
        Name = tagCategory;
        UpdateUIStates();
    }

    public void UpdateName(string name)
    {
        name = name.Trim();
        if (name == _name)
        {
            return;
        }

        _name = name;
        UpdateUIStates();
    }

    public void Save()
    {
        if (!_isNameValid)
        {
            return;
        }

        if (!IsSameCategory)
        {
            _ = TagInfoModel.RenameTagCategory(_oldName, _name);
        }
    }

    private void UpdateUIStates()
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
        return await TagInfoModel.Enqueue("MayOverwriteExistingEntries", () =>
        {
            SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
                .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory);
            command.PutQueryString(TagInfoTable.ColumnTagCategory);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                return true;
            }

            return false;
        });
    }
}
