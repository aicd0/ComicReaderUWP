// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReader.Common.Actions.Components;
using ComicReader.Common.Utils;

namespace ComicReader.Common.Actions.Providers;

internal class MessageDialogProvider : IActionProvider
{
    public const string NAME = "messagedialog";
    public const string PARAM_TITLE = "title";
    public const string PARAM_MESSAGE = "message";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IXamlRootProvider? xamlRootProvider = context.GetComponent<IXamlRootProvider>();
        if (xamlRootProvider == null)
        {
            context.SetError("No IXamlRootProvider component found.");
            return;
        }

        Microsoft.UI.Xaml.XamlRoot? xamlRoot = xamlRootProvider.GetXamlRoot();
        if (xamlRoot == null)
        {
            context.SetError("XamlRoot is null.");
            return;
        }

        string title = parameters[PARAM_TITLE] ?? "Title";
        string message = parameters[PARAM_MESSAGE] ?? "Message";
        DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder().SetTitle(title).SetContent(message).Build();
        _ = DialogUtils.ShowDialogAsync(xamlRoot, options);
    }
}
