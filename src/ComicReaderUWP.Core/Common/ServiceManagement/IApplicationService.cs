// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.ServiceManagement;

public interface IApplicationService : IService
{
    bool IsPortableBuild();

    string GetLocalFolderPath();

    string GetLocalCacheFolderPath();

    string GetTemporaryFolderPath();

    string GetEnvironmentDebugInfo();

    bool IsShuttingDown();
}
