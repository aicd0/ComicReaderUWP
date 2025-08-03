// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReader.Common.BaseUI;
using ComicReader.Views.Pages.Home;
using ComicReader.Views.Pages.Reader;
using ComicReader.Views.Pages.Search;
using ComicReader.Views.Pages.Settings;

namespace ComicReader.Helpers.Navigation;

internal class DefaultPageTrait : IPageTrait
{
    private readonly Type _pageType;

    public DefaultPageTrait(Type pageType)
    {
        _pageType = pageType;
    }

    public Type GetPageType()
    {
        return _pageType;
    }

    public bool HasNavigationBar()
    {
        return false;
    }

    public bool ImmersiveMode()
    {
        return false;
    }

    public bool SupportFullscreen()
    {
        return false;
    }

    public bool SupportMultiInstance()
    {
        return true;
    }
}

internal class HomePageTrait : IPageTrait
{
    private HomePageTrait() { }

    public Type GetPageType()
    {
        return typeof(HomePage);
    }

    public bool HasNavigationBar()
    {
        return true;
    }

    public bool ImmersiveMode()
    {
        return false;
    }

    public bool SupportFullscreen()
    {
        return false;
    }

    public bool SupportMultiInstance()
    {
        return true;
    }

    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new HomePageTrait();
            return _instance;
        }
    }
}

internal class SearchPageTrait : IPageTrait
{
    private SearchPageTrait() { }

    public Type GetPageType()
    {
        return typeof(SearchPage);
    }

    public bool HasNavigationBar()
    {
        return true;
    }

    public bool ImmersiveMode()
    {
        return false;
    }

    public bool SupportFullscreen()
    {
        return false;
    }

    public bool SupportMultiInstance()
    {
        return true;
    }

    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new SearchPageTrait();
            return _instance;
        }
    }
}

internal class ReaderPageTrait : IPageTrait
{
    private ReaderPageTrait() { }

    public Type GetPageType()
    {
        return typeof(ReaderPage);
    }

    public bool HasNavigationBar()
    {
        return true;
    }

    public bool ImmersiveMode()
    {
        return true;
    }

    public bool SupportFullscreen()
    {
        return true;
    }

    public bool SupportMultiInstance()
    {
        return true;
    }

    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new ReaderPageTrait();
            return _instance;
        }
    }
}

internal class SettingPageTrait : IPageTrait
{
    private SettingPageTrait() { }

    public Type GetPageType()
    {
        return typeof(SettingPage);
    }

    public bool HasNavigationBar()
    {
        return false;
    }

    public bool ImmersiveMode()
    {
        return false;
    }

    public bool SupportFullscreen()
    {
        return false;
    }

    public bool SupportMultiInstance()
    {
        return false;
    }

    private static IPageTrait _instance;
    public static IPageTrait Instance
    {
        get
        {
            _instance ??= new SettingPageTrait();
            return _instance;
        }
    }
}
