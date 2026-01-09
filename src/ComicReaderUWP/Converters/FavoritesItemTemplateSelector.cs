// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Converters;

public class FavoritesItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate NormalTemplate { get; set; }
    public DataTemplate RenamingTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        var model = item as FavoriteItemViewModel;
        return model.IsRenaming ? RenamingTemplate : NormalTemplate;
    }
};