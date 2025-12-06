// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.Utils;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Pages.SidePane.FilterPresets;

internal sealed partial class FilterPresetsPage : BasePage
{
    private readonly FilterPresetsPageViewModel ViewModel = new();

    public FilterPresetsPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ViewModel.Initialize(PageActionHandler);
        ViewModel.UpdateComics();
        ObserveData();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            ViewModel.UpdateComics();
        });

        GlobalEvent.Instance.FilterUpdated.Observe(this, delegate
        {
            ViewModel.UpdateComics();
        });

        ViewModel.FilterPresetDropDownLiveData.ObserveSticky(this, model =>
        {
            FilterPresetDropDownTextBlock.Text = model.Name;

            FlyoutBase flyout = FilterPresetDropDownButton.Flyout;
            if (flyout is not MenuFlyout)
            {
                flyout = new MenuFlyout
                {
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                };
                FilterPresetDropDownButton.Flyout = flyout;
            }

            var menuFlyout = (MenuFlyout)flyout;
            menuFlyout.Items.Clear();
            foreach (BaseMenuFlyoutItemViewModel item in model.Items)
            {
                menuFlyout.Items.Add(item.CreateMenuFlyoutItem());
            }
        });
    }

    //
    // Events
    //

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = (TagNodeViewModel)args.InvokedItem;
        item.OnClick?.Invoke();
    }

    private async void TreeView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.OriginalSource is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not TagNodeViewModel viewModel)
        {
            return;
        }

        FlyoutBase? flyout = await viewModel.CreateContextFlyout();
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

        args.Handled = true;
    }
}
