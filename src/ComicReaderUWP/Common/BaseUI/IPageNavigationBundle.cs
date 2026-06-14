// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.BaseUI;

internal interface IPageNavigationBundle
{
    public IPageTrait PageTrait { get; }

    public PageBundle Bundle { get; }

    public string Url { get; }

    public PageCommunicator Communicator { get; }
}
