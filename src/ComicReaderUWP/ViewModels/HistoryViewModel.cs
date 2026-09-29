// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.ViewModels;

internal class HistoryItemViewModel : BaseViewModel
{
    public ComicModel? Comic { get; set; }
    public long Id { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public string ItemGlyph => Comic is null ? "\uE9CE" : (Comic.IsCollection ? "\uF5ED" : "\uE8B9");
}

internal class HistoryGroupViewModel : ObservableCollection<HistoryItemViewModel>
{
    public HistoryGroupViewModel(string key) : base()
    {
        Key = key;
    }

    public string Key { get; set; }
}
