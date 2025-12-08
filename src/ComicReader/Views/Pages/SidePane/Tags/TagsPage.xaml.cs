// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.SDK.Common.Utils;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Dialogs.EditTagCategory;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.SidePane.Tags;

internal sealed partial class TagsPage : BasePage
{
    private readonly TagsPageViewModel ViewModel = new();

    public TagsPage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ViewModel.Initialize(PageActionHandler);
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

        ViewModel.EditTagCategoryLiveData.Observe(this, tagCategory =>
        {
            var dialog = new EditTagCateogoryDialog(tagCategory);
            _ = dialog.ShowAsync(WindowId);
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            _ = dialog.ShowAsync(WindowId);
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

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = ((TextBox)sender).Text;
        ViewModel.SetSearchText(text);
    }
}
