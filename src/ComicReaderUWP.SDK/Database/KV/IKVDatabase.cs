// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Database.KV;

public interface IKVDatabase
{
    IKVCollection GetCollection(string name);
}
