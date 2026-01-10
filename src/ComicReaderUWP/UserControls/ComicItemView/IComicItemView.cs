// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.UserControls.ComicItemView;

interface IComicItemView
{
    void SetComicModel(ComicItemViewModel? item);
}
