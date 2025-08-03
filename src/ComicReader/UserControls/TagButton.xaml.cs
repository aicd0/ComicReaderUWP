// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.UserControls;

internal sealed partial class TagButton : UserControl
{
    public TagViewModel? ViewModel => DataContext as TagViewModel;

    public TagButton()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OnClicked?.Invoke();
    }
}
