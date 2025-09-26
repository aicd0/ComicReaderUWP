// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Views.Pages.Main;

namespace ComicReader.Common.Actions.Components;

internal interface IMainPageAbilityComponent : IActionComponent
{
    IMainPageAbility Ability { get; }
}
