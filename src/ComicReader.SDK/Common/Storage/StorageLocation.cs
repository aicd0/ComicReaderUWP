// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.ServiceManagement;

using Windows.Storage;

namespace ComicReader.SDK.Common.Storage;

public static class StorageLocation
{
    private const string DIR_USER = "user";

    private static readonly Lazy<bool> _portable = new(() =>
    {
        return ServiceManager.GetService<IApplicationService>().IsPortableBuild();
    });

    private static readonly Lazy<string> _localFolderPath = new(() =>
    {
        if (_portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local");
        }
        else
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
    });

    private static readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        if (_portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local_cache");
        }
        else
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
    });

    private static readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        if (_portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "temporary");
        }
        else
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
    });

    public static string LocalFolderPath => _localFolderPath.Value;
    public static string LocalCacheFolderPath => _localCacheFolderPath.Value;
    public static string TemporaryFolderPath => _temporaryFolderPath.Value;

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }
}
