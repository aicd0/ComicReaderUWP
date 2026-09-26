// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class ComicTagsView : UserControl
{
    public ComicTagsView()
    {
        InitializeComponent();
    }

    public IReadOnlyList<TagCollectionViewModel>? Tags
    {
        get => (IReadOnlyList<TagCollectionViewModel>?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(
        nameof(Tags),
        typeof(IReadOnlyList<TagCollectionViewModel>),
        typeof(ComicTagsView),
        new PropertyMetadata(null));
}
