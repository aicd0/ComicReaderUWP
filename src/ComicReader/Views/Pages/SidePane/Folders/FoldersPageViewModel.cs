// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.SidePane.Folders;

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

        CoroutineUtils.Start(async () =>
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
        CoroutineUtils.Start(async () =>
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

        SimpleTreeViewNodeModel ComicToNode(ComicModel comic)
        {
            return new()
            {
                DataContext = comic,
                Glyph = "\uE8B9",
                Title = comic.Title,
                CanExpand = false,
                Clicked = () =>
                {
                    Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                        .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .Build();
                    _actionHandler.Handle(actionModel);
                },
                RequestContextMenuItemsAsync = (primary, selection) =>
                {
                    IEnumerable<ComicModel> selectedComics = selection.Where(x => x.DataContext is ComicModel).Select(x => (ComicModel)x.DataContext!);
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(comic, _actionHandler, selectedComics, canSelect: !SelectionMode);
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
            foreach (ComicModel comic in sortedComics)
            {
                nodes.Add(ComicToNode(comic));
            }

            return nodes;
        }

        return createNodes(rootNode);
    }

    private async Task<List<BaseMenuFlyoutItemModel>> CreateFolderMenuItems(SimpleTreeViewNodeModel primary, IEnumerable<SimpleTreeViewNodeModel> selection)
    {
        List<ComicModel> comics = [.. primary.CollectDataContext<ComicModel>()];
        ComicModel? randomComic = comics.Count > 0 ? comics[Random.Shared.Next(comics.Count)] : null;
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(_actionHandler, randomComic,
            primary.ExpandAll, primary.CollapseAll);
    }

    private class FolderNode
    {
        public required string Name { get; init; }
        public List<ComicModel> Comics { get; } = [];
        public Dictionary<string, FolderNode> Folders { get; } = [];
    }
}
