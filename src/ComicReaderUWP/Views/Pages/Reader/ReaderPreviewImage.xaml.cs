// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderPreviewImage : UserControl
{
    public ReaderImagePreviewViewModel Model { get; set; }

    public ReaderPreviewImage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    public void SetModel(ReaderImagePreviewViewModel model, bool inRecycleQueue)
    {
        if (inRecycleQueue)
        {
            ImageHolder.SetModel(null);
        }

        Model = model;

        if (!inRecycleQueue)
        {
            ImageHolder.SetModel(model.Image);
        }
    }
}
