// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReaderUWP.ViewModels;

internal partial class ComicGroupViewModel(string groupName, IEnumerable<ComicItemViewModel> items, bool collapsed) : SimpleGroupViewModel<ComicItemViewModel>(groupName, items, collapsed)
{
    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            NotifyPropertyChanged(nameof(Description));
        }
    }
}
