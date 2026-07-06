// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Sidebar.Favorite;

internal sealed partial class FavoritePage : BasePage
{
    private ObservableCollection<FavoriteItemViewModel> DataSource { get; set; } = [];

    public FavoritePage()
    {
        InitializeComponent();
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        Update();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            Update();
        });
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    // utilities
    private void Update()
    {
        FavoriteModel.ExternalModel model = FavoriteModel.Instance.GetModel();
        if (model == null)
        {
            return;
        }

        static void fillNode(List<FavoriteModel.ExternalNodeModel> it, ObservableCollection<FavoriteItemViewModel> et, FavoriteItemViewModel? parent)
        {
            foreach (FavoriteModel.ExternalNodeModel inode in it)
            {
                FavoriteNodeType type = inode.Type == "i" ? FavoriteNodeType.Item : FavoriteNodeType.Filter;
                var enode = new FavoriteItemViewModel(inode.Name, type);

                if (type == FavoriteNodeType.Filter)
                {
                    fillNode(inode.Children, enode.Children, enode);
                }
                else
                {
                    enode.Id = inode.Id;
                }

                et.Add(enode);
            }
        }

        ObservableCollection<FavoriteItemViewModel> items = [];
        fillNode(model.Children, items, null);

        static bool comparer(FavoriteItemViewModel x, FavoriteItemViewModel y) => x.Type == y.Type && x.Id == y.Id;
        static void updater(FavoriteItemViewModel x, FavoriteItemViewModel y)
        {
            x.Name = y.Name;
            DiffUtils.UpdateCollection(x.Children, y.Children, comparer, updater);
        }

        DiffUtils.UpdateCollection(DataSource, items, comparer, updater);
        foreach (FavoriteItemViewModel item in DataSource)
        {
            item.FixParent();
        }

        UpdateView();
    }

    private void UpdateView()
    {
        TbNoFavorite.Visibility = DataSource.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save()
    {
        void helper(List<FavoriteModel.ExternalNodeModel> it, ObservableCollection<FavoriteItemViewModel> et)
        {
            foreach (FavoriteItemViewModel enode in et)
            {
                var inode = new FavoriteModel.ExternalNodeModel(
                    enode.Type == FavoriteNodeType.Filter ? "f" : "i",
                    enode.Name,
                    enode.Id,
                    []
                );

                if (enode.Children.Count > 0)
                {
                    helper(inode.Children, enode.Children);
                }

                it.Add(inode);
            }
        }

        FavoriteModel.ExternalModel model = new([]);
        helper(model.Children, DataSource);
        FavoriteModel.Instance.UpdateModel(model);
    }

    private void DeleteItem(FavoriteItemViewModel item)
    {
        if (item.Parent != null)
        {
            item.Parent.Children.Remove(item);
        }
        else
        {
            DataSource.Remove(item);
        }

        Save();
        UpdateView();
    }

    private async Task ResetItems()
    {
        async Task<bool> helper(ObservableCollection<FavoriteItemViewModel> root)
        {
            foreach (FavoriteItemViewModel item in root)
            {
                if (item.IsRenaming)
                {
                    if (item.EditingName.Length == 0)
                    {
                        item.EditingName = item.Name;
                    }
                    else
                    {
                        item.Name = item.EditingName;
                    }

                    item.IsRenaming = false;
                    ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
                    C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
                    Save();
                    return true;
                }

                if (item.Expanded && item.Type == FavoriteNodeType.Filter)
                {
                    if (await helper(item.Children))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        await helper(DataSource);
        UpdateView();
    }

    private void CreateNewFolder(ObservableCollection<FavoriteItemViewModel> folder, FavoriteItemViewModel? parent)
    {
        string folderName;
        string new_folder_string = StringResourceProvider.Instance.NewFolder;

        for (int folderIndex = 1; folderIndex < 65536; ++folderIndex)
        {
            if (folderIndex > 1)
            {
                folderName = new_folder_string + " (" + folderIndex.ToString() + ")";
            }
            else
            {
                folderName = new_folder_string;
            }

            bool isDuplicated = false;

            foreach (FavoriteItemViewModel item in folder)
            {
                if (item.Name == folderName)
                {
                    isDuplicated = true;
                    break;
                }
            }

            if (!isDuplicated)
            {
                var newFolder = new FavoriteItemViewModel(folderName, FavoriteNodeType.Filter)
                {
                    IsRenaming = true
                };

                folder.Add(newFolder);
                break;
            }
        }

        parent?.FixParent();
        UpdateView();
    }

    private void SortFavorites(ObservableCollection<FavoriteItemViewModel> source)
    {
        var ordered = source.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.Name), StringUtils.SmartFileNameComparer).ToList();

        for (int i = 0; i < ordered.Count; ++i)
        {
            FavoriteItemViewModel item = ordered[i];

            if (source.IndexOf(item) == i)
            {
                continue;
            }

            source.Remove(item);
            source.Insert(i, item);
        }

        Save();
    }

    //
    // Events
    //

    private void MainTreeViewBackgroundPressed(object sender, PointerRoutedEventArgs e)
    {
        CoroutineUtils.Run(ResetItems);
    }

    private void MainTreeViewItemPressed(object sender, PointerRoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var item = (Microsoft.UI.Xaml.Controls.TreeViewItem)sender;
            var ctx = (FavoriteItemViewModel)item.DataContext;

            if (ctx.IsRenaming)
            {
                return;
            }

            await ResetItems();
            e.Handled = true;
        });
    }

    private void MainTreeViewItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var item = (FavoriteItemViewModel)e.InvokedItem;

            if (item.IsRenaming)
            {
                return;
            }

            await ResetItems();

            if (item.Type != FavoriteNodeType.Item)
            {
                return;
            }

            ComicModel? comic = await ComicModel.FromId(item.Id);
            if (comic is null)
            {
                DeleteItem(item);
                return;
            }

            IEnumerable<long> playlistComicIds = item.Parent?.Children.Where(x => x.Type == FavoriteNodeType.Item).Select(x => x.Id) ?? [];
            PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComicIds(playlistComicIds);
            Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
            OpenComicHelper.OpenComic(PageActionHandler, route);
            GetMainPageAbility().SetSidePaneOpenState(false, force: false);
        });
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
        DeleteItem(item);
    }

    private void RenameClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
        item.IsRenaming = true;
        ObservableCollection<FavoriteItemViewModel> parent = item.Parent != null ? item.Parent.Children : DataSource;
        C1<FavoriteItemViewModel>.NotifyCollectionChanged(parent, item);
    }

    private void RenameTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        var item = (FavoriteItemViewModel)textBox.DataContext;
        item.EditingName = textBox.Text;
    }

    private void RenameTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await ResetItems();
                e.Handled = true;
            }
        });
    }

    private void RenameTextBoxDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        var ctx = (FavoriteItemViewModel)args.NewValue;

        if (ctx.IsRenaming)
        {
            var textbox = (TextBox)sender;
            textbox.Focus(FocusState.Programmatic);
            textbox.SelectAll();
        }
    }

    private void NewFolderClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;

        if (item.Type == FavoriteNodeType.Filter)
        {
            if (!item.Expanded)
            {
                item.Expanded = true;
                C1<FavoriteItemViewModel>.NotifyCollectionChanged(item.Parent != null ? item.Parent.Children : DataSource, item);
            }

            CreateNewFolder(item.Children, item);
        }
        else
        {
            if (item.Parent != null)
            {
                CreateNewFolder(item.Parent.Children, item.Parent);
            }
            else
            {
                CreateNewFolder(DataSource, null);
            }
        }

        Save();
    }

    private void RootNewFolderClick(object sender, RoutedEventArgs e)
    {
        CreateNewFolder(DataSource, null);
        Save();
    }

    private void MainTreeViewDragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
    {
        var parent = (FavoriteItemViewModel)args.NewParentItem;
        foreach (FavoriteItemViewModel item in args.Items.Cast<FavoriteItemViewModel>())
        {
            item.Parent = parent;
        }

        Save();
    }

    private void OpenInNewTabClick(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
            ComicModel? comic = await ComicModel.FromId(item.Id);
            if (comic is null)
            {
                DeleteItem(item);
                return;
            }

            IEnumerable<long> playlistComicIds = item.Parent?.Children.Where(x => x.Type == FavoriteNodeType.Item).Select(x => x.Id) ?? [];
            PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComicIds(playlistComicIds);
            Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
            GetMainPageAbility().OpenInNewTab(route);
        });
    }

    private void SortByNameClick(object sender, RoutedEventArgs e)
    {
        var item = (FavoriteItemViewModel)((MenuFlyoutItem)sender).DataContext;
        ObservableCollection<FavoriteItemViewModel> parent = item.Type == FavoriteNodeType.Filter ? item.Children :
            (item.Parent != null ? item.Parent.Children : DataSource);
        SortFavorites(parent);
    }

    private void RootSortByNameClick(object sender, RoutedEventArgs e)
    {
        SortFavorites(DataSource);
    }
}
