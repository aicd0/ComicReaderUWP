// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class CustomActionProvider(CustomActionProvider.IHandler handler) : IActionProvider
{
    public const string NAME = "custom";
    public const string PARAM_SOURCE = "source";
    public const string PARAM_NAME = "name";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        string source = parameters[PARAM_SOURCE] ?? string.Empty;
        string name = parameters[PARAM_NAME] ?? string.Empty;

        List<string> args = [];
        for (int i = 0; true; i++)
        {
            string pKey = $"p{i}";
            string? pValue = parameters[pKey];
            if (pValue == null)
            {
                break;
            }

            args.Add(pValue);
        }

        handler.Handle(source, name, args);
        context.SetSuccess();
    }

    public interface IHandler
    {
        void Handle(string source, string name, IReadOnlyList<string> args);
    }
}
