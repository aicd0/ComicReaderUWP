// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.Views.Pages.SidePane.FilterPresets;

internal sealed partial class FilterPresetsPage : BasePage
{
    private const string TAG = nameof(FilterPresetsPage);

    private readonly FilterPresetsPageViewModel ViewModel = new();

    public FilterPresetsPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        PageActionHandler.RegisterProvider(new CustomActionProvider(new CustomActionHandler(ViewModel)));

        ObserveData();
        ViewModel.Initialize(PageActionHandler);
        ViewModel.UpdateComics();
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

    //
    // Types
    //

    private class CustomActionHandler(FilterPresetsPageViewModel viewModel) : CustomActionProvider.IHandler
    {
        public void Handle(string source, string name, IReadOnlyList<string> args)
        {
            bool handled = true;
            switch (source)
            {
                case MenuFlyoutItemsCreator.CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU:
                    switch (name)
                    {
                        case MenuFlyoutItemsCreator.CUSTOM_ACTION_NAME_SELECT:
                            viewModel.SelectionMode = true;
                            break;
                        default:
                            handled = false;
                            break;
                    }
                    break;
                default:
                    handled = false;
                    break;
            }

            if (!handled)
            {
                Logger.F(TAG, $"Unknown action '{source}.{name}'");
            }
        }
    }
}
