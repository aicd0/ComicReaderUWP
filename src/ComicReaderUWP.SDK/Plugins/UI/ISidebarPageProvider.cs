// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins.UI;

public interface ISidebarPageProvider
{
    string Name { get; }

    string Host { get; }

    IconElement Icon { get; }
}
