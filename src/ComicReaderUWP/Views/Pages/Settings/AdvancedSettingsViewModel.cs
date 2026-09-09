// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.IO;

using ComicReaderUWP.Common.Archive;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class AdvancedSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(AdvancedSettingsViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsSharedViewModel _shared = new();
    public SettingsSharedViewModel Shared
    {
        get => _shared;
        private set
        {
            _shared = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shared)));
        }
    }

    private bool _isClearingCache = false;
    public bool IsClearingCache
    {
        get => _isClearingCache;
        set
        {
            _isClearingCache = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsClearingCache)));
        }
    }

    private string _cacheSize = StringResourceProvider.Instance.Calculating;
    public string CacheSize
    {
        get => _cacheSize;
        set
        {
            _cacheSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClearCacheText)));
        }
    }

    private bool _sendUsageData;
    public bool SendUsageData
    {
        get => _sendUsageData;
        set
        {
            _sendUsageData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendUsageData)));
        }
    }

    public string ClearCacheText
    {
        get => $"{StringResourceProvider.Instance.ClearCache} ({_cacheSize})";
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Shared.UpdateStarted += Update;
    }

    public void ClearCache()
    {
        IsClearingCache = true;
        TaskDispatcher.DefaultQueue.Submit(() =>
        {
            ClearCacheInternal();
            string size = GetCacheSize();
            CoroutineUtils.RunInMainThread(() =>
            {
                IsClearingCache = false;
                CacheSize = size;
            });
        });
    }

    public void RefreshRandomSeed()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.GetModel();
        model.ComicShuffleRandomSeed = Random.Shared.Next();
        AppSettingsModel.UpdateModel(model);
    }

    private void Update()
    {
        UpdateBasicSettings();
        UpdateCacheSize();
    }

    private void UpdateBasicSettings()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            SendUsageData = AppSettingsModel.SendUsageData;
        });
    }

    private void UpdateCacheSize()
    {
        string size = GetCacheSize();
        CoroutineUtils.RunInMainThread(() =>
        {
            CacheSize = size;
        });
    }

    //
    // File cache
    //

    private static string GetCacheSize()
    {
        long size = 0;
        size += GetCacheDirectorySize(StorageLocation.LocalCacheFolderPath);
        size += GetCacheDirectorySize(StorageLocation.TemporaryFolderPath);

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        if (size < 1024)
        {
            return $"{size} B";
        }

        int unitIndex = (int)Math.Floor(Math.Log(size, 1024));
        double adjustedSize = size / Math.Pow(1024, unitIndex);
        return $"{adjustedSize:0.#} {units[unitIndex]}";
    }

    private static void ClearCacheInternal()
    {
        ImageLoader.Clear();
        ArchiveCacheManager.Clear();
        ClearCacheDirectory(StorageLocation.LocalCacheFolderPath);
        ClearCacheDirectory(StorageLocation.TemporaryFolderPath);
    }

    private static long GetCacheDirectorySize(string directoryPath)
    {
        long size = 0;

        DirectoryInfo directory;
        try
        {
            directory = new(directoryPath);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return size;
        }

        FileInfo[] files;
        try
        {
            files = directory.GetFiles();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "GetCacheSize", ex);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                size += file.Length;
            }
            catch (Exception ex)
            {
                Logger.E(TAG, "GetCacheSize", ex);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "GetCacheSize", ex);
            dirs = [];
        }

        foreach (DirectoryInfo dir in dirs)
        {
            if (dir.Name == "Local")
            {
                continue;
            }

            size += FileUtils.GetApproximateDirectorySize(dir);
        }

        return size;
    }

    private static void ClearCacheDirectory(string directoryPath)
    {
        DirectoryInfo directory;
        try
        {
            directory = new(directoryPath);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return;
        }

        FileInfo[] files;
        try
        {
            files = directory.GetFiles();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "ClearDirectory", ex);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                Logger.E(TAG, "ClearDirectory", ex);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "ClearDirectory", ex);
            dirs = [];
        }

        foreach (DirectoryInfo dir in dirs)
        {
            try
            {
                dir.Delete(true);
            }
            catch (Exception ex)
            {
                Logger.E(TAG, "ClearDirectory", ex);
            }
        }
    }
}
