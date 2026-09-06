// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.ServiceManagement.Services;

namespace ComicReaderUWP.Core.Tests.Common;

internal class DebugService : IDebugService
{
    public string DebugCommandPublicKeyPem => string.Empty;

    public bool SentryEnabled => false;

    public void OnCrashReport(string info)
    {
        Assert.Fail(info);
    }

    public bool HandleDebugCommand(string command)
    {
        return false;
    }
}
