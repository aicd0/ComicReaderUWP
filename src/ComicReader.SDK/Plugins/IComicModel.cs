// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Plugins;

public interface IComicModel
{
    string Description { get; }
    int Rating { get; }

    void SetRating(int rating);
}
