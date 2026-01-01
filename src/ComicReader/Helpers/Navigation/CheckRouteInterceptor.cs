// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Helpers.Navigation;

internal class CheckRouteInterceptor : IRouterInterceptor
{
    private const string TAG = nameof(CheckRouteInterceptor);

    public bool Intercept(Route route, out NavigationBundle? navigationBundle)
    {
        navigationBundle = null;

        if (route.Scheme != RouterConstants.SCHEME_APP_NO_PREFIX)
        {
            Logger.F(TAG, $"Invalid scheme {route.Scheme}");
            return true;
        }

        if (route.Port != -1)
        {
            Logger.F(TAG, $"Invalid port {route.Port}");
            return true;
        }

        if (route.Path.Length > 0 && route.Path != "/")
        {
            Logger.F(TAG, $"Invalid path {route.Path}");
            return true;
        }

        if (route.Fragment.Length > 0)
        {
            Logger.F(TAG, $"Invalid fragment {route.Fragment}");
            return true;
        }

        return false;
    }
}
