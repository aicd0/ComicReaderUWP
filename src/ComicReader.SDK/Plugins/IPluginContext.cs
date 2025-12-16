// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Plugins;

public interface IPluginContext
{
    void RegisterComicEditedHandler(IComicEditedHandler handler);
}
