// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Comic;

public interface IComicModel
{
    long Id { get; }

    string Location { get; }

    int PageCount { get; }

    string Title1 { get; }

    string Title2 { get; }

    string Description { get; }

    int Rating { get; }

    IReadOnlyList<IComicTagCategory> Tags { get; }

    IReadOnlyDictionary<string, string> Links { get; }

    bool IsHidden { get; }

    CompletionStatusEnum CompletionStatus { get; }

    Task SetTitle1(string title);

    Task SetTitle2(string title);

    Task SetDescription(string description);

    Task SetRating(int rating);

    Task SetTags(IReadOnlyDictionary<string, HashSet<string>> tags);

    Task SetLinks(IReadOnlyDictionary<string, string> links);

    Task SetHidden(bool isHidden);

    Task SetCompletionStatus(CompletionStatusEnum status);

    Task<IComicConnection?> Open();

    Task<bool> MoveToLocation(string location);
}
