// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Lifecycle;
using ComicReader.Common.Utils;
using ComicReader.Data;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Views.Dialogs.EditTagCategory;

internal partial class EditTagCateogoryDialogViewModel
{
    public MutableLiveData<string> NameLiveData = new();
    public MutableLiveData<bool> SaveEnableLiveData = new();

    private string _oldName = string.Empty;
    private string _name = string.Empty;
    private bool _isNameValid = false;

    public void Initialize(string tagCategory)
    {
        _oldName = tagCategory;
        UpdateName(tagCategory);
        NameLiveData.Emit(tagCategory);
    }

    public void UpdateName(string name)
    {
        name = name.Trim();
        if (name == _name)
        {
            return;
        }

        _name = name;
        _isNameValid = name.Length > 0;
        UpdateButtonStates();
    }

    public void Save()
    {
        if (!_isNameValid || _name == _oldName)
        {
            return;
        }

        CoroutineUtils.Start(async () =>
        {
            List<long> comicIds = [];
            await ComicData.Enqueue("SaveTagCategory", () =>
            {
                SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                    .AppendCondition(TagCategoryTable.ColumnName, _oldName);
                IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
                SelectCommand.IReader reader = command.Execute(SqlDatabaseManager.MainDatabase);
                while (reader.Read())
                {
                    long comicId = comicIdToken.GetValue();
                    comicIds.Add(comicId);
                }

                return true;
            });

            List<ComicModel> comics = await ComicModel.BatchFromId("SaveTagCategory", comicIds);
            foreach (ComicModel comic in comics)
            {
                Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
                if (!tags.TryGetValue(_name, out HashSet<string>? tagSet))
                {
                    tagSet = [];
                    tags[_name] = tagSet;
                }

                if (tags.TryGetValue(_oldName, out HashSet<string>? oldTagSet))
                {
                    tags.Remove(_oldName);
                    foreach (string tag in oldTagSet)
                    {
                        tagSet.Add(tag);
                    }
                }

                comic.SetTags(tags);
            }
        });
    }

    private void UpdateButtonStates()
    {
        bool isInputValid = _isNameValid;
        SaveEnableLiveData.Emit(isInputValid);
    }
}
