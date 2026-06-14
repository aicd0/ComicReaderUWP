// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ComicReaderUWP.Helpers.Navigation;

internal static class AppRouter
{
    private static readonly OpenPageInterceptor _openPageInterceptor = new();

    private static readonly List<IRouterInterceptor> sInterceptors =
    [
        new CheckRouteInterceptor(),
        _openPageInterceptor,
    ];

    public static PageNavigationBundle? Process(Route route)
    {
        foreach (IRouterInterceptor interceptor in sInterceptors)
        {
            if (interceptor.Intercept(route, out PageNavigationBundle? bundle))
            {
                return bundle;
            }
        }

        return null;
    }

    public static void RegisterPage(string host, Type pageType)
    {
        _openPageInterceptor.RegisterPage(host, new DefaultPageTrait(pageType));
    }
}
