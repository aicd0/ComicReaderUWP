// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Actions.Components;

internal class WeakXamlRootProvider(UIElement element) : IXamlRootProvider
{
    private readonly WeakReference<UIElement> _uiElementReference = new(element);

    public XamlRoot? GetXamlRoot()
    {
        if (_uiElementReference.TryGetTarget(out UIElement? element))
        {
            return element.XamlRoot;
        }

        return null;
    }
}
