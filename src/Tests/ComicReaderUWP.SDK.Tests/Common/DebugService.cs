// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.ServiceManagement;

namespace ComicReaderUWP.Core.Tests.Common;

internal class DebugService : IDebugService
{
    public void OnCrashReport(string info)
    {
        Assert.Fail(info);
    }

    public bool HandleDebugCommand(string command)
    {
        return false;
    }
}
