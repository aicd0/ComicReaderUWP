// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Actions.Components;

internal class MainPageComponent(int tabId) : IMainPageComponent
{
    public int TabId => tabId;
}
