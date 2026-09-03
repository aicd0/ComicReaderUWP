// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderPreviewImage : UserControl
{
    public ReaderPreviewImageViewModel? ViewModel { get; set; }

    public ReaderPreviewImage()
    {
        InitializeComponent();
    }

    private void ReaderPreviewImage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        ViewModel = args.NewValue as ReaderPreviewImageViewModel;

        Bindings.Update();

        if (ViewModel is null)
        {
            ImageHolder.SetModel(null);
        }
        else
        {
            ImageHolder.SetModel(ViewModel.Image);
        }
    }

    private void ReaderPreviewImage_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        Func<Task<IReadOnlyList<BaseMenuFlyoutItemModel>>>? requestFunc = ViewModel?.RequestContextMenu;
        if (requestFunc is null)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Run(async () =>
        {
            IReadOnlyList<BaseMenuFlyoutItemModel> menuItems = await requestFunc();

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            if (flyout is null)
            {
                return;
            }

            if (args.TryGetPosition(fe, out Windows.Foundation.Point point))
            {
                flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
            }
            else
            {
                flyout.ShowAt(fe);
            }
        });
    }
}
