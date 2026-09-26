// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.Views.Dialogs.EditTag;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.ViewModels;

internal partial class TagCollectionViewModel(string name) : BaseViewModel, INotifyPropertyChanged
{
    public static readonly TagCollectionViewModel Default = new(string.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public ObservableCollection<TagViewModel> Tags { get; } = [];

    public override void NotifyImmediately()
    {
        for (int i = 0; i < Tags.Count; i++)
        {
            Tags[i] = Tags[i];
        }
    }

    //
    // Construction
    //

    public static void Update(ObservableCollection<TagCollectionViewModel> target, ComicModel? comic, ActionHandler actionHandler)
    {
        List<TagCollectionViewModel> newCollection = Create(comic, actionHandler);
        DiffUtils.UpdateCollection(target, newCollection, (x, y) => x.Name == y.Name, (x, y) =>
        {
            DiffUtils.UpdateCollection(x.Tags, y.Tags, (a, b) => a.Tag == b.Tag, (a, b) =>
            {
                a.OnRequestContextFlyoutAsync = b.OnRequestContextFlyoutAsync;
                a.OnClicked = b.OnClicked;
            });
        });
    }

    private static List<TagCollectionViewModel> Create(ComicModel? comic, ActionHandler actionHandler)
    {
        List<TagCollectionViewModel> newCollection = [];
        if (comic is null)
        {
            return newCollection;
        }

        foreach (KeyValuePair<string, ComicTagCategory> tagItem in comic.Tags)
        {
            string tagCategory = tagItem.Key;
            List<TagViewModel> tagModels = [];
            foreach (string tag in tagItem.Value.Tags)
            {
                string tagName = tag;
                TagViewModel tagModel = new()
                {
                    Tag = tagName,
                    OnClicked = () => OpenTagSearch(actionHandler, tagCategory, tagName),
                    OnRequestContextFlyoutAsync = () => CreateTagContextMenuItems(comic, actionHandler, tagCategory, tagName),
                };
                tagModels.Add(tagModel);
            }

            tagModels.Sort((a, b) => string.Compare(a.Tag, b.Tag, ignoreCase: true));
            var tagCollectionModel = new TagCollectionViewModel(tagItem.Key);
            foreach (TagViewModel tagModel in tagModels)
            {
                tagCollectionModel.Tags.Add(tagModel);
            }

            newCollection.Add(tagCollectionModel);
        }

        newCollection.Sort((a, b) => string.Compare(a.Name, b.Name, ignoreCase: true));
        return newCollection;
    }

    private static void OpenTagSearch(ActionHandler actionHandler, string tagCategory, string tag)
    {
        string expression = $"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(tagCategory)}\"=\"{ExpressionUtils.EscapeString(tag)}\"";
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
            .WithParam(RouterConstants.ARG_KEYWORD, $"exp:\"{ExpressionUtils.EscapeString(expression)}\"");
        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
            .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
            .Build();
        actionHandler.HandleNoResult(actionModel);
    }

    private static async Task<List<BaseMenuFlyoutItemModel>> CreateTagContextMenuItems(ComicModel comic, ActionHandler actionHandler, string tagCategory, string tag)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Links,
            Icon = new FontIconSource() { Glyph = "\uE71B" },
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(tagCategory, tag, actionHandler),
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Edit,
            Icon = new FontIconSource() { Glyph = "\uE70F" },
            Click = () =>
            {
                if (!actionHandler.TryGetComponent(out IMainWindowComponent? mainWindowCom))
                {
                    return;
                }

                var dialog = new EditTagDialog(tagCategory, tag);
                CoroutineUtils.Run(() => dialog.ShowAsync(mainWindowCom.WindowId));
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Delete,
            Icon = new FontIconSource() { Glyph = "\uE74D" },
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
                    if (tags.TryGetValue(tagCategory, out HashSet<string>? tagSet))
                    {
                        if (tagSet.Remove(tag))
                        {
                            await comic.SetTags(tags);
                        }
                    }
                });
            },
        });

        return items;
    }
};
