// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI.PageAbilities;

namespace ComicReaderUWP.Common.BaseUI;

internal class PageCommunicator
{
    private readonly Dictionary<Type, IPageAbility> _abilities = [];

    public void RegisterAbility<T>(T ability) where T : IPageAbility
    {
        _abilities.Add(typeof(T), ability);
    }

    public T? GetAbility<T>() where T : class
    {
        if (_abilities.TryGetValue(typeof(T), out IPageAbility? value))
        {
            return (T)value;
        }

        return null;
    }
}
