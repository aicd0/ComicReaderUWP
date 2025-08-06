// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.BaseUI;

namespace ComicReader.Helpers.Navigation;

internal class CheckRouteInterceptor : IRouterInterceptor
{
    public NavigationBundle? Intercept(Route route)
    {
        if (route.Scheme != RouterConstants.SCHEME_APP_NO_PREFIX)
        {
            throw new ArgumentException($"Invalid scheme {route.Scheme}");
        }

        if (route.Port != -1)
        {
            throw new ArgumentException($"Invalid port {route.Port}");
        }

        if (route.Path.Length > 0 && route.Path != "/")
        {
            throw new ArgumentException($"Invalid path {route.Path}");
        }

        if (route.Fragment.Length > 0)
        {
            throw new ArgumentException($"Invalid fragment {route.Fragment}");
        }

        return null;
    }
}
