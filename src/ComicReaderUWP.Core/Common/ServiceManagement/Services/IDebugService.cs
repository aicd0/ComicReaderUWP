// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.ServiceManagement.Services;

public interface IDebugService : IService
{
    string DebugCommandPublicKeyPem { get; }

    bool SentryEnabled { get; }

    void OnCrashReport(string info);

    bool HandleDebugCommand(string command);
}
