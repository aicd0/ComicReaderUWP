// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;
using ComicReader.ViewModels;
using ComicReader.Views.Dialogs.EditComicInfo;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Dialogs.EditTagCategory;
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.SidePane.Tags;

internal sealed partial class TagsPage : BasePage
{
    private readonly TagsPageViewModel ViewModel = new();

    public TagsPage()
    {
        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();

        ObserveData();
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

        ViewModel.EditComicLiveData.Observe(this, comics =>
        {
            CoroutineUtils.Start(async () =>
            {
                if (comics.Count == 0)
                {
                    return;
                }

                var dialog = new EditComicInfoDialog(comics);
                ContentDialogResult result = await dialog.ShowAsync(XamlRoot);
                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.UpdateTags();
                }
            });
        });

        ViewModel.EditTagCategoryLiveData.Observe(this, tagCategory =>
        {
            var dialog = new EditTagCateogoryDialog(tagCategory);
            _ = dialog.ShowAsync(XamlRoot);
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            _ = dialog.ShowAsync(XamlRoot);
        });
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    //
    // Events
    //

    private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var item = (TreeNodeViewModel)args.InvokedItem;
        item.OnClick?.Invoke();
    }
}
