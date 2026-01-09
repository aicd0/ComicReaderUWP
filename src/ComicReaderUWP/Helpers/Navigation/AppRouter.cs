// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReaderUWP.Helpers.Navigation;

internal static class AppRouter
{
    private static readonly List<IRouterInterceptor> sInterceptors = new()
    {
        new CheckRouteInterceptor(),
        new OpenPageInterceptor(),
    };

    public static NavigationBundle? Process(Route route)
    {
        foreach (IRouterInterceptor interceptor in sInterceptors)
        {
            if (interceptor.Intercept(route, out NavigationBundle? bundle))
            {
                return bundle;
            }
        }

        return null;
    }
}
