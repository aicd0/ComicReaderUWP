// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Actions;

internal interface IActionProviderContext
{
    T? GetComponent<T>() where T : IActionComponent;

    void SetError(string message);

    void SetSuccess();
}
