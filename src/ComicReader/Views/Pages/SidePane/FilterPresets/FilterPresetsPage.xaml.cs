// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.Utils;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = ((TextBox)sender).Text;
        ViewModel.SetSearchText(text);
    }
}
