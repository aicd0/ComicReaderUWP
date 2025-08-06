// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.Common.Services;

internal class DebugService : IDebugService
{
    public bool HandleDebugCommand(string command)
    {
        if (command == "dev_tools")
        {
            MainWindow.Open(url: RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS);
            return true;
        }

        return false;
    }
}
