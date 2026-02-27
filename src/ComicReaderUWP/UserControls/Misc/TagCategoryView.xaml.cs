// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class TagCategoryView : BaseUserControl
{
    public TagCollectionViewModel ViewModel => DataContext as TagCollectionViewModel ?? TagCollectionViewModel.Default;
    private TagCollectionViewModel? ViewModelNullable => DataContext as TagCollectionViewModel;

    public TagCategoryView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            Bindings.StopTracking();
            Bindings.Update();
            ViewModelNullable?.NotifyImmediately();
        };
    }
}
