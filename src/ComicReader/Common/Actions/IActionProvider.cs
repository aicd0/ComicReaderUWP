// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

namespace ComicReader.Common.Actions;

internal interface IActionProvider
{
    string Name { get; }

    void Handle(IActionProviderContext context, NameValueCollection parameters);
}
