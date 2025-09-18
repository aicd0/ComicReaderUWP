// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Actions;

internal interface IActionCallback
{
    void OnSuccess();

    void OnError(string message);
}
