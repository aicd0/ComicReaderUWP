// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.IO;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

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
        TaskDispatcher.DefaultQueue.Submit("ClearCache", delegate
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
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        model.ComicShuffleRandomSeed = Random.Shared.Next();
        AppSettingsModel.Instance.UpdateModel(model);
    }

    private void Update()
    {
        UpdateCacheSize();
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
        ImageCacheManager.Clear();
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
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return size;
        }

        FileInfo[] files;
        try
        {
            files = directory.GetFiles();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "GetCacheSize", e);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                size += file.Length;
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetCacheSize", e);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "GetCacheSize", e);
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
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return;
        }

        FileInfo[] files;
        try
        {
            files = directory.GetFiles();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "ClearDirectory", e);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                Logger.E(TAG, "ClearDirectory", e);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "ClearDirectory", e);
            dirs = [];
        }

        foreach (DirectoryInfo dir in dirs)
        {
            try
            {
                dir.Delete(true);
            }
            catch (Exception e)
            {
                Logger.E(TAG, "ClearDirectory", e);
            }
        }
    }
}
