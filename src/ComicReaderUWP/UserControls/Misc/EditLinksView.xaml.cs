// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class EditLinksView : UserControl
{
    private const string TAG = nameof(EditLinksView);

    private ObservableCollection<LinkItemViewModel> _links = [];
    public ObservableCollection<LinkItemViewModel> Links
    {
        get => _links;
        set
        {
            _links = value;
            Bindings.Update();
        }
    }

    public EditLinksView()
    {
        InitializeComponent();
    }

    private void RemoveLinkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var item = (LinkItemViewModel)((Button)sender).DataContext;
        RemoveLink(item);
    }

    private void AddLinkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AddLink();
    }

    private void AddLink()
    {
        if (Links.Count == 0)
        {
            Logger.F(TAG, "Cannot add link, collection cannot be empty");
            return;
        }

        Links.Insert(Links.Count - 1, new()
        {
            IsPlaceholder = false,
            Name = string.Empty,
            Link = string.Empty,
        });
    }

    private void RemoveLink(LinkItemViewModel item)
    {
        Links.Remove(item);
    }
}
