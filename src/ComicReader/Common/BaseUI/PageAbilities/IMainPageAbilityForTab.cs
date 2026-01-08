// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.BaseUI.PageAbilities;

internal interface IMainPageAbilityForTab : IMainPageAbility
{
    int TabId { get; }

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void SetUrl(string url);
}
