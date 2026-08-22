// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Threading.Tasks;

namespace ComicReaderUWP.Common.Actions;

internal interface IActionProvider
{
    string Name { get; }

    Task<ActionResult> Handle(IActionProviderContext context, NameValueCollection parameters);
}
