// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.SDK.Tests.Common;

internal class DebugService : IDebugService
{
    public bool HandleDebugCommand(string command)
    {
        return false;
    }
}
