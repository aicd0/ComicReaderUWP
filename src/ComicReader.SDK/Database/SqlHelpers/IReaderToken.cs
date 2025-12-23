// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Database.SqlHelpers;

public interface IReaderToken<T>
{
    T GetValue();
}
