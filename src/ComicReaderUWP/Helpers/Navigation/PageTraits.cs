// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Views.Pages.Reader;
using ComicReaderUWP.Views.Pages.Settings;

namespace ComicReaderUWP.Helpers.Navigation;

internal class DefaultPageTrait(Type pageType) : IPageTrait
{
    private readonly Type _pageType = pageType;

    public Type PageType => _pageType;

    public bool IsImmersiveMode => false;

    public bool AllowMultiplePages => true;
}

internal class ReaderPageTrait : IPageTrait
{
    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new ReaderPageTrait();
            return _instance;
        }
    }

    private ReaderPageTrait() { }

    public Type PageType => typeof(ReaderPage);

    public bool IsImmersiveMode => true;

    public bool AllowMultiplePages => true;
}

internal class SettingsPageTrait : IPageTrait
{
    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new SettingsPageTrait();
            return _instance;
        }
    }

    private SettingsPageTrait() { }

    public Type PageType => typeof(SettingsPage);

    public bool IsImmersiveMode => false;

    public bool AllowMultiplePages => false;
}
