// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Threading;

public interface ITaskDispatcher
{
    Task Submit(Action action);

    Task<R> Submit<R>(Func<R> func);

    Task SubmitAsync(Func<Task> func);

    Task<R> SubmitAsync<R>(Func<Task<R>> func);
}
