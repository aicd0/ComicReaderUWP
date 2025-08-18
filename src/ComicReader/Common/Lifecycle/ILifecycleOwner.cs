// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Lifecycle;

public interface ILifecycleOwner
{
    ILifecycle GetLifecycle();
}
