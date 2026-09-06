// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Views.Dialogs.EditComicInfo;
using ComicReaderUWP.Views.Dialogs.EditTag;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Sidebar.ComicInfo;

internal sealed partial class ComicInfoPage : BasePage
{
    private const string REGEX_URL = @"https?:\/\/[a-zA-Z0-9\-._~%]+(?:\.[a-zA-Z0-9\-._~%]+)+(?:\/[^\s]*)?";

    private readonly ComicInfoPageViewModel ViewModel = new();

    public ComicInfoPage()
    {
        InitializeComponent();

        ViewModel.ComicTitle1 = "";
        ViewModel.ComicTitle2 = "";
        ViewModel.ComicDir = "";
        ViewModel.IsEditable = false;
    }

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        ViewModel.Initialize(PageActionHandler);
        ObserveData();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            ViewModel.Reload();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            ViewModel.Reload();
        });

        GlobalEvent.Instance.TagInfoUpdated.Observe(this, delegate
        {
            ViewModel.Reload();
        });

        GetWindowEventBus().With<ComicChangedEventArgs>(EventId.ComicInfoChanged).ObserveSticky(this, args =>
        {
            ViewModel.SetComic(args.Comic);
            ViewModel.SetImageDescriptions(args.ImageDescriptions);
            ViewModel.Playlist = args.Playlist;
        });

        GetWindowEventBus().With<PlaybackModel>(EventId.PlaybackChanged).ObserveSticky(this, playback =>
        {
            ViewModel.Playback = playback;
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            CoroutineUtils.Run(() => dialog.ShowAsync(WindowId));
        });

        ViewModel.CompletionStatusLiveData.ObserveSticky(this, completionStatus =>
        {
            SetCompletionStateButton.Label = CompletionStatusService.EnumToString(completionStatus);
        });

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
        });

        ViewModel.ComicDescriptionLiveData.ObserveSticky(this, description =>
        {
            FillRichTextInlines(TbComicDescription.Inlines, description);
            TbComicDescription.Visibility = TbComicDescription.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        });
    }

    private void SetCompletionStateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        ComicModel? comic = ViewModel.Comic;
        if (comic is null)
        {
            return;
        }

        List<BaseMenuFlyoutItemModel> menuItems = MenuFlyoutItemsCreator.CreateCompletionStatusMenuItems([comic]);

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Top });
    }

    private void MoreAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            if (sender is not FrameworkElement fe)
            {
                return;
            }

            ComicModel? comic = ViewModel.Comic;
            if (comic is null)
            {
                return;
            }

            List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicMenuItems(
                PageActionHandler,
                comic,
                playlist: ViewModel.Playlist.ToBuilder(),
                playback: ViewModel.Playback?.ToBuilder());
            if (menuItems.Count == 0)
            {
                return;
            }

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            flyout.ShowAt(fe, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        });
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        int value = (int)sender.Value;
        ViewModel.Comic?.SetRating(value < 1 ? -1 : value * 20);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            ErrorResult err = await ErrorLogger.Run(nameof(OnDirectoryTapped), async err =>
            {
                ComicModel? comic = ViewModel.Comic;
                if (comic is null)
                {
                    return err.Error("Comic is null.");
                }

                ErrorResult innerErr = await comic.ShowInFileExplorer();
                if (!innerErr.IsSuccessful)
                {
                    return err.Error(innerErr);
                }

                return err.Success();
            });

            err.DisplayErrorMessage(PageActionHandler);
        });
    }

    private void OnEditInfoClick(object sender, RoutedEventArgs e)
    {
        ComicModel? comic = ViewModel.Comic;
        if (comic == null)
        {
            return;
        }

        var dialog = new EditComicInfoDialog([comic]);
        CoroutineUtils.Run(() => dialog.ShowAsync(WindowId));
    }

    //
    // New tags
    //

    private readonly Lazy<SearchHistoryModel> _tagHistoryModel = new(() =>
    {
        return SearchHistoryModel.Get("NewTags");
    });

    private void NewTagsAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var autoSuggestBox = (AutoSuggestBox)sender;
        autoSuggestBox.ItemsSource = SearchTagHistory(autoSuggestBox.Text);
    }

    private void NewTagsAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string text = sender.Text.Trim();
        sender.Text = string.Empty;
        if (ViewModel.AddNewTags(text))
        {
            _tagHistoryModel.Value.Save(text);
        }
    }

    private void NewTagsAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string text = args.SelectedItem.ToString() ?? string.Empty;
        sender.Text = string.Empty;
        if (ViewModel.AddNewTags(text))
        {
            _tagHistoryModel.Value.Save(text);
        }
    }

    private void NewTagsAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = SearchTagHistory(sender.Text);
        }
    }

    private void NewTagTipButton_Click(object sender, RoutedEventArgs e)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("NewTagsHelp.txt", StringResourceProvider.Instance.EnterNewTagsHint);
    }

    private List<string> SearchTagHistory(string query)
    {
        char[] seperators = [.. LocalizationUtils.Colons, .. LocalizationUtils.Commas];
        string[] keywords = query.Split(seperators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return [.. _tagHistoryModel.Value.Search(keywords, 10)];
    }

    //
    // Utilities
    //

    private static void FillRichTextInlines(InlineCollection inlines, string richText)
    {
        inlines.Clear();

        Regex urlRegex = URL_REGEX();
        MatchCollection matches = urlRegex.Matches(richText);
        int currentIndex = 0;

        foreach (Match match in matches)
        {
            Uri uri;
            try
            {
                uri = new Uri(match.Value);
            }
            catch (Exception)
            {
                continue;
            }

            int startIndex = match.Index;
            int endIndex = match.Index + match.Length;
            if (endIndex <= currentIndex)
            {
                continue;
            }

            if (startIndex > currentIndex)
            {
                var run = new Run
                {
                    Text = richText[currentIndex..startIndex]
                };
                inlines.Add(run);
            }

            {
                var run = new Run
                {
                    Text = match.Value
                };

                var hyperlink = new Hyperlink
                {
                    NavigateUri = uri
                };

                hyperlink.Inlines.Add(run);
                inlines.Add(hyperlink);
            }

            currentIndex = endIndex;
        }

        if (currentIndex < richText.Length)
        {
            var run = new Run
            {
                Text = richText[currentIndex..]
            };

            inlines.Add(run);
        }
    }

    [GeneratedRegex(REGEX_URL, RegexOptions.None)]
    private static partial Regex URL_REGEX();
}
