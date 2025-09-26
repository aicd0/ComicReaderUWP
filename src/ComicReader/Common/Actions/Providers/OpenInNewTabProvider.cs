// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReader.Common.Actions.Components;
using ComicReader.Helpers.Navigation;

namespace ComicReader.Common.Actions.Providers;

internal class OpenInNewTabProvider : IActionProvider
{
    public const string NAME = "OpenInNewTab";
    public const string PARAM_URL = "URL";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IMainPageAbilityComponent? abilityCom = context.GetComponent<IMainPageAbilityComponent>();
        if (abilityCom is null)
        {
            context.SetError("No IMainPageAbilityComponent component found.");
            return;
        }

        string url = parameters[PARAM_URL] ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            context.SetError("No url parameter found.");
            return;
        }

        var route = Route.Create(url);
        abilityCom.Ability.OpenInNewTab(route);
        context.SetSuccess();
    }
}
