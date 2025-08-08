// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Windows.ApplicationModel.Resources;

namespace ComicReader.Common;

public class StringResourceProvider
{
    private static readonly ResourceLoader sResourceLoader = new();

    public static StringResourceProvider Instance { get; } = new();

    //// SECTION MARKER - DO NOT MOVE ////
    public string About => GetResourceString("About");
    public string AboutCopyright => GetResourceString("AboutCopyright");
    public string Add => GetResourceString("Add");
    public string AddFolder => GetResourceString("AddFolder");
    public string AddToFavorites => GetResourceString("AddToFavorites");
    public string AllComics => GetResourceString("AllComics");
    public string AllComicsIn => GetResourceString("AllComicsIn");
    public string AllHidden => GetResourceString("AllHidden");
    public string AllMatchedResults => GetResourceString("AllMatchedResults");
    public string AllPages => GetResourceString("AllPages");
    public string AppDescription => GetResourceString("AppDescription");
    public string AppDisplayName => GetResourceString("AppDisplayName");
    public string AppStoreName => GetResourceString("AppStoreName");
    public string ApplyOnNextLaunch => GetResourceString("ApplyOnNextLaunch");
    public string Ascending => GetResourceString("Ascending");
    public string Background => GetResourceString("Background");
    public string BackgroundAcrylic => GetResourceString("BackgroundAcrylic");
    public string Calculating => GetResourceString("Calculating");
    public string Cancel => GetResourceString("Cancel");
    public string Category => GetResourceString("Category");
    public string ClearCacheDetail => GetResourceString("ClearCacheDetail");
    public string CollapseAll => GetResourceString("CollapseAll");
    public string ComicInfo => GetResourceString("ComicInfo");
    public string CompletionState => GetResourceString("CompletionState");
    public string ContributionRunAfterLink => GetResourceString("ContributionRunAfterLink");
    public string ContributionRunBeforeLink => GetResourceString("ContributionRunBeforeLink");
    public string DebugModeWarning => GetResourceString("DebugModeWarning");
    public string Default => GetResourceString("Default");
    public string DefaultTags => GetResourceString("DefaultTags");
    public string Delete => GetResourceString("Delete");
    public string Descending => GetResourceString("Descending");
    public string Description => GetResourceString("Description");
    public string DevAppDisplayName => GetResourceString("DevAppDisplayName");
    public string DiffMode => GetResourceString("DiffMode");
    public string Done => GetResourceString("Done");
    public string Edit => GetResourceString("Edit");
    public string ExpandAll => GetResourceString("ExpandAll");
    public string ExpressionAnd => GetResourceString("ExpressionAnd");
    public string ExpressionIn => GetResourceString("ExpressionIn");
    public string ExpressionInvalid => GetResourceString("ExpressionInvalid");
    public string ExpressionNot => GetResourceString("ExpressionNot");
    public string ExpressionOr => GetResourceString("ExpressionOr");
    public string ExpressionReference => GetResourceString("ExpressionReference");
    public string ExpressionValid => GetResourceString("ExpressionValid");
    public string Favorite => GetResourceString("Favorite");
    public string Favorites => GetResourceString("Favorites");
    public string FilterSettings => GetResourceString("FilterSettings");
    public string FilteredBy => GetResourceString("FilteredBy");
    public string FinishPercentage => GetResourceString("FinishPercentage");
    public string Finished => GetResourceString("Finished");
    public string FunctionAverage => GetResourceString("FunctionAverage");
    public string FunctionItemCount => GetResourceString("FunctionItemCount");
    public string FunctionMax => GetResourceString("FunctionMax");
    public string FunctionMin => GetResourceString("FunctionMin");
    public string FunctionSum => GetResourceString("FunctionSum");
    public string GoBack => GetResourceString("GoBack");
    public string GoForward => GetResourceString("GoForward");
    public string Group => GetResourceString("Group");
    public string Help => GetResourceString("Help");
    public string Hide => GetResourceString("Hide");
    public string History => GetResourceString("History");
    public string LastReadTime => GetResourceString("LastReadTime");
    public string LinkErrorContent => GetResourceString("LinkErrorContent");
    public string LinkErrorTitle => GetResourceString("LinkErrorTitle");
    public string Links => GetResourceString("Links");
    public string MarkAsRead => GetResourceString("MarkAsRead");
    public string MarkAsReading => GetResourceString("MarkAsReading");
    public string MarkAsUnread => GetResourceString("MarkAsUnread");
    public string Name => GetResourceString("Name");
    public string NewFolder => GetResourceString("NewFolder");
    public string NewTab => GetResourceString("NewTab");
    public string NoRating => GetResourceString("NoRating");
    public string NoResults => GetResourceString("NoResults");
    public string NoTagsHint => GetResourceString("NoTagsHint");
    public string None => GetResourceString("None");
    public string OK => GetResourceString("OK");
    public string OpenInFileExplorer => GetResourceString("OpenInFileExplorer");
    public string OpenInNewTab => GetResourceString("OpenInNewTab");
    public string OpenRandomComic => GetResourceString("OpenRandomComic");
    public string OverwriteExistingEntries => GetResourceString("OverwriteExistingEntries");
    public string PageGap => GetResourceString("PageGap");
    public string PageLayoutDualNoCover => GetResourceString("PageLayoutDualNoCover");
    public string PageLayoutDualNoCoverMirrored => GetResourceString("PageLayoutDualNoCoverMirrored");
    public string PageLayoutDualWithCover => GetResourceString("PageLayoutDualWithCover");
    public string PageLayoutDualWithCoverMirrored => GetResourceString("PageLayoutDualWithCoverMirrored");
    public string PageLayoutSingle => GetResourceString("PageLayoutSingle");
    public string Proceed => GetResourceString("Proceed");
    public string Progress => GetResourceString("Progress");
    public string Rating => GetResourceString("Rating");
    public string ReaderStatusError => GetResourceString("ReaderStatusError");
    public string ReaderStatusLoading => GetResourceString("ReaderStatusLoading");
    public string Reading => GetResourceString("Reading");
    public string Refresh => GetResourceString("Refresh");
    public string Remove => GetResourceString("Remove");
    public string RemoveFromFavorites => GetResourceString("RemoveFromFavorites");
    public string Save => GetResourceString("Save");
    public string SaveAsDefaultConfig => GetResourceString("SaveAsDefaultConfig");
    public string SearchResults => GetResourceString("SearchResults");
    public string SearchResultsOf => GetResourceString("SearchResultsOf");
    public string Select => GetResourceString("Select");
    public string SetCompletionState => GetResourceString("SetCompletionState");
    public string Settings => GetResourceString("Settings");
    public string ShowTagId => GetResourceString("ShowTagId");
    public string Sort => GetResourceString("Sort");
    public string SortingFunction => GetResourceString("SortingFunction");
    public string Statistics => GetResourceString("Statistics");
    public string Tag => GetResourceString("Tag");
    public string Tags => GetResourceString("Tags");
    public string TagsColon => GetResourceString("TagsColon");
    public string TextWithColon => GetResourceString("TextWithColon");
    public string Title => GetResourceString("Title");
    public string Title1 => GetResourceString("Title1");
    public string Title1Colon => GetResourceString("Title1Colon");
    public string Title2 => GetResourceString("Title2");
    public string Title2Colon => GetResourceString("Title2Colon");
    public string TotalComics => GetResourceString("TotalComics");
    public string Unfavorite => GetResourceString("Unfavorite");
    public string Ungrouped => GetResourceString("Ungrouped");
    public string Unhide => GetResourceString("Unhide");
    public string Unread => GetResourceString("Unread");
    public string Untitled => GetResourceString("Untitled");
    public string UseSystemLanguage => GetResourceString("UseSystemLanguage");
    public string ViewType => GetResourceString("ViewType");
    public string ViewTypeLarge => GetResourceString("ViewTypeLarge");
    public string ViewTypeMedium => GetResourceString("ViewTypeMedium");
    public string Warning => GetResourceString("Warning");
    //// SECTION MARKER - DO NOT MOVE ////

    private StringResourceProvider() { }

    public string WithColon(string text)
    {
        return TextWithColon.Replace("$text", text);
    }

    private static string GetResourceString(string resource)
    {
        return sResourceLoader.GetString(resource) ?? "?";
    }
}
