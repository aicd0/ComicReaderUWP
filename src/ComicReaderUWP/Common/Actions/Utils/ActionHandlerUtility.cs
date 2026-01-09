// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Actions.Providers;

namespace ComicReaderUWP.Common.Actions.Utils;

internal class ActionHandlerUtility
{
    private ActionHandlerUtility()
    {
    }

    public static void RegisterCommonProviders(ActionHandler handler)
    {
        handler.RegisterProvider(new MessageDialogProvider());
        handler.RegisterProvider(new OpenTabProvider());
        handler.RegisterProvider(new EditComicProvider());
        handler.RegisterProvider(new FullscreenServiceProvider());
    }
}
