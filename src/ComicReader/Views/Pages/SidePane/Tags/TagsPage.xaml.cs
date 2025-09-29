// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;
using ComicReader.ViewModels;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Dialogs.EditTagCategory;
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

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
