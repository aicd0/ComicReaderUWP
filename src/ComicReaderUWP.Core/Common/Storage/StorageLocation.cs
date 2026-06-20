// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;

namespace ComicReaderUWP.Core.Common.Storage;

public static class StorageLocation
{
    private static readonly Lazy<string> _localFolderPath = new(() =>
    {
        return ServiceManager.GetService<IApplicationService>().GetLocalFolderPath();
    });

    private static readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        return ServiceManager.GetService<IApplicationService>().GetLocalCacheFolderPath();
    });

    private static readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        return ServiceManager.GetService<IApplicationService>().GetTemporaryFolderPath();
    });

    public static string LocalFolderPath => _localFolderPath.Value;
    public static string LocalCacheFolderPath => _localCacheFolderPath.Value;
    public static string TemporaryFolderPath => _temporaryFolderPath.Value;
    public static string RegistryFolderPath => Path.Combine(LocalFolderPath, "reg");
}
