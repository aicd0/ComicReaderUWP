// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.SDK.Tests.Common;

internal class ApplicationService : IApplicationService
{
    public string GetEnvironmentDebugInfo()
    {
        return string.Empty;
    }

    public bool IsPortableBuild()
    {
        return true;
    }
}
