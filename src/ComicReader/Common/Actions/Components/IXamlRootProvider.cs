// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Actions.Components;

internal interface IXamlRootProvider : IActionComponent
{
    XamlRoot? GetXamlRoot();
}
