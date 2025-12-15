// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Plugins;

public interface IBeforeComicUpdatingHandler
{
    void OnComicUpdating(IComicModel comic);
}
