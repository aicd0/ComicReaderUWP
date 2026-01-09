// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReaderUWP.ViewModels;

internal class ReaderImageViewModel
{
    public BitmapImage? Image { get; set; } = null;
    public bool ImageRequested { get; set; } = false;
}
