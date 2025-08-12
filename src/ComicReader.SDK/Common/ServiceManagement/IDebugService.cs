// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.ServiceManagement;

public interface IDebugService : IService
{
    void OnCrashReport(string info);

    bool HandleDebugCommand(string command);
}
