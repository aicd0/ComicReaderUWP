// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.BaseUI;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Dialogs.EditTagCategory;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.SidePane.Tags;

internal sealed partial class TagsPage : BasePage
{
    private const string TAG = nameof(TagsPage);

    private readonly TagsPageViewModel ViewModel = new();

    public TagsPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        PageActionHandler.RegisterProvider(new CustomActionProvider(new CustomActionHandler(ViewModel)));

        ObserveData();
        ViewModel.Initialize(PageActionHandler);
        ViewModel.UpdateTags();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.TagInfoUpdated.Observe(this, delegate
        {
            ViewModel.UpdateTags();
        });

        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            ViewModel.UpdateTags();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            ViewModel.UpdateTags();
        });

        ViewModel.OpenInCurrentTabLiveData.Observe(this, route =>
        {
            GetMainPageAbility().OpenInCurrentTab(route);
        });

        ViewModel.OpenInNewTabLiveData.Observe(this, route =>
        {
            GetMainPageAbility().OpenInNewTab(route);
        });

        ViewModel.EditTagCategoryLiveData.Observe(this, tagCategory =>
        {
            var dialog = new EditTagCateogoryDialog(tagCategory);
            CoroutineUtils.Start(() => dialog.ShowAsync(WindowId));
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            CoroutineUtils.Start(() => dialog.ShowAsync(WindowId));
        });
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
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

    private class CustomActionHandler(TagsPageViewModel viewModel) : CustomActionProvider.IHandler
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
                            viewModel.SelectionMode = !viewModel.SelectionMode;
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
