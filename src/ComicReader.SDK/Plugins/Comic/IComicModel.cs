// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Plugins.Comic;

public interface IComicModel
{
    string Description { get; }

    int Rating { get; }

    Task SetDescription(string description);

    Task SetRating(int rating);

    Task SetCompletionStatus(CompletionStatusEnum status);
}
