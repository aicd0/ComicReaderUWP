// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Actions.Components;

internal class MainWindowComponent(int windowId) : IMainWindowComponent
{
    public int WindowId => windowId;
}
