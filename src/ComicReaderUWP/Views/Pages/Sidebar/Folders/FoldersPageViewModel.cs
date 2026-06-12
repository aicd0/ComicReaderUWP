// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.Sidebar.Folders;

internal partial class FoldersPageViewModel : INotifyPropertyChanged
{
    private const int SEARCH_DELAY = 200;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SimpleTreeViewNodeModel> DataSource { get; set; } = [];

    public bool _selectionMode = false;
    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionMode)));
        }
    }

    private bool _noComicsVisible = false;
    public bool NoComicsVisible
    {
        get => _noComicsVisible;
        set
        {
            if (_noComicsVisible != value)
            {
                _noComicsVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoComicsVisible)));
            }
        }
    }

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.Factory.NewQueue("FilterPresetsPageQueue");
    private readonly ComicSearchEngine _searchEngine = new();
    private int _searchSubmitted = 0;

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnComicSearchResult);
    }

    public void UpdateComics()
    {
        ScheduleUpdateComics();
    }

    public void SetSearchText(string searchText)
    {
        searchText = searchText.Trim();
        if (searchText == _searchEngine.SearchText)
        {
            return;
        }

        _searchEngine.SearchText = searchText;
        if (Interlocked.CompareExchange(ref _searchSubmitted, 1, 0) == 1)
        {
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(SEARCH_DELAY);
            Interlocked.Exchange(ref _searchSubmitted, 0);
            _searchEngine.Update();
        });
    }

    private void ScheduleUpdateComics()
    {
        _searchEngine.Update();
    }

    private void OnComicSearchResult(IReadOnlyList<ComicModel> items)
    {
        CoroutineUtils.Run(async () =>
        {
            List<SimpleTreeViewNodeModel> dataSource = await GenerateNodeTree(items);

            void UpdateItem(SimpleTreeViewNodeModel from, SimpleTreeViewNodeModel to)
            {
                from.Glyph = to.Glyph;
                from.Description = to.Description;
                from.CanExpand = to.CanExpand;
                from.Clicked = to.Clicked;
                from.RequestContextMenuItemsAsync = to.RequestContextMenuItemsAsync;
                DiffUtils.UpdateCollection(from.Children, to.Children, (a, b) => a.Title == b.Title, UpdateItem);
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                DiffUtils.UpdateCollection(DataSource, dataSource, (x, y) => x.Title == y.Title, UpdateItem);
                NoComicsVisible = DataSource.Count == 0;
            });
        });
    }

    private async Task<List<SimpleTreeViewNodeModel>> GenerateNodeTree(IReadOnlyList<ComicModel> comics)
    {
        FolderNode rootNode = new() { Name = string.Empty };
        foreach (ComicModel comic in comics)
        {
            FolderNode currentNode = rootNode;
            IReadOnlyList<string> folders = comic.FolderViewPath;
            foreach (string folder in folders)
            {
                if (!currentNode.Folders.TryGetValue(folder, out FolderNode? folderNode))
                {
                    folderNode = new() { Name = folder };
                    currentNode.Folders[folder] = folderNode;
                }

                currentNode = folderNode;
            }

            currentNode.Comics.Add(comic);
        }

        SimpleTreeViewNodeModel ComicToNode(ComicModel comic, PlaylistModel.Builder playlist)
        {
            return new()
            {
                DataContext = comic,
                Glyph = "\uE8B9",
                Title = comic.Title,
                CanExpand = false,
                Clicked = item =>
                {
                    OpenComicHelper.OpenComic(_actionHandler, OpenComicHelper.GetComicRoute(comic, playlist));
                },
                RequestContextMenuItemsAsync = (primary, selection) =>
                {
                    IEnumerable<ComicModel>? selectedComics = !_selectionMode ? null : selection
                        .Where(x => x.DataContext is ComicModel)
                        .Select(x => (ComicModel)x.DataContext!);
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler, comic, playlist,
                        selectedComics: selectedComics, canSelect: !SelectionMode);
                },
            };
        }

        List<SimpleTreeViewNodeModel> createNodes(FolderNode folderNode)
        {
            List<SimpleTreeViewNodeModel> nodes = [];

            IEnumerable<string> folderNames = folderNode.Folders.Keys
                .OrderBy(StringUtils.SmartFileNameKeySelector, StringUtils.SmartFileNameComparer);
            foreach (string folder in folderNames)
            {
                FolderNode subFolderNode = folderNode.Folders[folder];
                List<SimpleTreeViewNodeModel> subNodes = createNodes(subFolderNode);
                SimpleTreeViewNodeModel item = new()
                {
                    Glyph = "\uE8B7",
                    Title = subFolderNode.Name,
                    CanExpand = true,
                    IsExpanded = false,
                    RequestContextMenuItemsAsync = CreateFolderMenuItems,
                };
                foreach (SimpleTreeViewNodeModel node in subNodes)
                {
                    item.Children.Add(node);
                }

                nodes.Add(item);
            }

            IEnumerable<ComicModel> sortedComics = folderNode.Comics
                .OrderBy(x => StringUtils.SmartFileNameKeySelector(x.Title), StringUtils.SmartFileNameComparer);
            PlaylistModel.Builder playlist = PlaylistModel.Builder.Create().AddComics(sortedComics);
            foreach (ComicModel comic in sortedComics)
            {
                nodes.Add(ComicToNode(comic, playlist));
            }

            return nodes;
        }

        return createNodes(rootNode);
    }

    private async Task<List<BaseMenuFlyoutItemModel>> CreateFolderMenuItems(SimpleTreeViewNodeModel primary, IEnumerable<SimpleTreeViewNodeModel> selection)
    {
        List<ComicModel> comics = [.. primary.CollectDataContext<ComicModel>()];
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(
            _actionHandler, comics,
            primary.ExpandAll, primary.CollapseAll);
    }

    private class FolderNode
    {
        public required string Name { get; init; }
        public List<ComicModel> Comics { get; } = [];
        public Dictionary<string, FolderNode> Folders { get; } = [];
    }
}
