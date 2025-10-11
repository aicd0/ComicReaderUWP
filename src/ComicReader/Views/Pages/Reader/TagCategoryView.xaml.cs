// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.Reader;

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
