// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.ServiceManagement;

public interface IApplicationService : IService
{
    bool IsPortableBuild();

    string GetEnvironmentDebugInfo();
}
