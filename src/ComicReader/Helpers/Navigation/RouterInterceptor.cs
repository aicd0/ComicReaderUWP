// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;

namespace ComicReader.Helpers.Navigation;

internal interface IRouterInterceptor
{
    NavigationBundle? Intercept(Route route);
}
