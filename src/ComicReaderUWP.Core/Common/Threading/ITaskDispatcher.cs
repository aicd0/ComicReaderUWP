// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Threading;

public interface ITaskDispatcher
{
    void Submit(Action action);

    void Submit(string taskName, Action action);
}
