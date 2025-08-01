// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.Common.Services;

internal class ApplicationService : IApplicationService
{
    public bool IsPortableBuild()
    {
#if PORTABLE
        return true;
#else
        return false;
#endif
    }

    public string GetEnvironmentDebugInfo()
    {
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        return sb.ToString();
    }
}
