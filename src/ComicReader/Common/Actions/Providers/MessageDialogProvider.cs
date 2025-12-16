// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReader.Common.Actions.Components;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.Utils;

namespace ComicReader.Common.Actions.Providers;

internal class MessageDialogProvider : IActionProvider
{
    public const string NAME = "messagedialog";
    public const string PARAM_TITLE = "title";
    public const string PARAM_MESSAGE = "message";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
        if (mainWindowCom is null)
        {
            context.SetError("No IMainWindowComponent component found.");
            return;
        }

        string title = parameters[PARAM_TITLE] ?? "Untitled";
        string message = parameters[PARAM_MESSAGE] ?? "(no message provided)";
        DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
            .SetTitle(title)
            .SetContent(message)
            .Build();
        CoroutineUtils.Start(() => DialogUtils.EnqueueDialogAsync(mainWindowCom.WindowId, options));
        context.SetSuccess();
    }
}
