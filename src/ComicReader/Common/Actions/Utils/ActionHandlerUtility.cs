// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Actions.Providers;

namespace ComicReader.Common.Actions.Utils;

internal class ActionHandlerUtility
{
    private ActionHandlerUtility()
    {
    }

    public static void RegisterCommonProviders(ActionHandler handler)
    {
        handler.RegisterProvider(new MessageDialogProvider());
        handler.RegisterProvider(new OpenInNewTabProvider());
        handler.RegisterProvider(new EditComicProvider());
    }
}
