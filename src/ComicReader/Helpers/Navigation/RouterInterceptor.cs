// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Helpers.Navigation;

internal interface IRouterInterceptor
{
    bool Intercept(Route route, out NavigationBundle? navigationBundle);
}
