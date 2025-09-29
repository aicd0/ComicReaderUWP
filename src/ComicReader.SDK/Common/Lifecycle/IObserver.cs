// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public interface IObserver<T>
{
    void OnChanged(T value);
}
