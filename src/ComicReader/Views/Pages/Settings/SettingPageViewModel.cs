// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using ComicReader.Common.Imaging;
using ComicReader.Common.Localization;
using ComicReader.Common.Misc;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.Misc;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.SqlHelpers;

using Windows.Globalization;

namespace ComicReader.Views.Pages.Settings;

public partial class SettingPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(SettingPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ReaderWriterLock _lock = new();
    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.DefaultQueue;
    private AppSettingsModel.ExternalModel? _settingsModel;
    private bool _languageChanged = false;

    private List<Tuple<string, int>> _encodings = [];
    public List<Tuple<string, int>> Encodings
    {
        get => _encodings;
        set
        {
            _encodings = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Encodings)));
        }
    }

    private bool _scanOnLaunch = true;
    public bool ScanOnLaunch
    {
        get => _scanOnLaunch;
        set
        {
            _scanOnLaunch = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanOnLaunch)));
        }
    }

    private bool _removeUnreachableComics = true;
    public bool RemoveUnreachableComics
    {
        get => _removeUnreachableComics;
        set
        {
            _removeUnreachableComics = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveUnreachableComics)));
        }
    }

    private bool _restoreLastReadingPosition = true;
    public bool RestoreLastReadingPosition
    {
        get => _restoreLastReadingPosition;
        set
        {
            _restoreLastReadingPosition = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RestoreLastReadingPosition)));
        }
    }

    private bool _promptBeforeRemovingComics = true;
    public bool PromptBeforeRemovingComics
    {
        get => _promptBeforeRemovingComics;
        set
        {
            _promptBeforeRemovingComics = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PromptBeforeRemovingComics)));
        }
    }

    private int _defaultArchiveCodePageIndex = 0;
    public int DefaultArchiveCodePageIndex
    {
        get => _defaultArchiveCodePageIndex;
        set
        {
            _defaultArchiveCodePageIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DefaultArchiveCodePageIndex)));

            int selectedIndex = value;
            if (selectedIndex >= 0 && selectedIndex < Encodings.Count)
            {
                AppModel.DefaultArchiveCodePage = Encodings[selectedIndex].Item2;
            }
        }
    }

    private List<BackgroundEntry> _backgrounds = [];
    public List<BackgroundEntry> Backgrounds
    {
        get => _backgrounds;
        set
        {
            _backgrounds = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Backgrounds)));
        }
    }

    private int _backgroundIndex = 0;
    public int BackgroundIndex
    {
        get => _backgroundIndex;
        set
        {
            _backgroundIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundIndex)));
        }
    }

    private List<LanguageEntry> _languages = [];
    public List<LanguageEntry> Languages
    {
        get => _languages;
        set
        {
            _languages = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Languages)));
        }
    }

    private int _languageIndex = 0;
    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            _languageIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));
        }
    }

    private bool _transitionAnimation = true;
    public bool TransitionAnimation
    {
        get => _transitionAnimation;
        set
        {
            _transitionAnimation = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransitionAnimation)));

            AppModel.TransitionAnimation = value;
        }
    }

    private bool _automaticallyHideCursor = true;
    public bool AutomaticallyHideCursor
    {
        get => _automaticallyHideCursor;
        set
        {
            _automaticallyHideCursor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticallyHideCursor)));

            AppModel.AutomaticallyHideCursor = value;
        }
    }

    private bool _antiAliasingEnabled = true;
    public bool AntiAliasingEnabled
    {
        get => _antiAliasingEnabled;
        set
        {
            _antiAliasingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AntiAliasingEnabled)));

            AppModel.AntiAliasingEnabled = value;
        }
    }

    private bool _isClearHistoryEnabled = false;
    public bool IsClearHistoryEnabled
    {
        get => _isClearHistoryEnabled;
        set
        {
            _isClearHistoryEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsClearHistoryEnabled)));
        }
    }

    private bool _historySaveBrowsingHistory = false;
    public bool HistorySaveBrowsingHistory
    {
        get => _historySaveBrowsingHistory;
        set
        {
            _historySaveBrowsingHistory = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HistorySaveBrowsingHistory)));

            AppModel.SaveBrowsingHistory = value;
        }
    }

    private bool _appearanceLightChecked = false;
    public bool AppearanceLightChecked
    {
        get => _appearanceLightChecked;
        set
        {
            if (value == _appearanceLightChecked)
            {
                return;
            }

            _appearanceLightChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceLightChecked)));

            if (value)
            {
                SetAppearance(AppSettingsModel.AppearanceSetting.Light);
            }
        }
    }

    private bool _appearanceDarkChecked = false;
    public bool AppearanceDarkChecked
    {
        get => _appearanceDarkChecked;
        set
        {
            if (value == _appearanceDarkChecked)
            {
                return;
            }

            _appearanceDarkChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceDarkChecked)));

            if (value)
            {
                SetAppearance(AppSettingsModel.AppearanceSetting.Dark);
            }
        }
    }

    private bool _appearanceUseSystemSettingChecked = false;
    public bool AppearanceUseSystemSettingChecked
    {
        get => _appearanceUseSystemSettingChecked;
        set
        {
            if (value == _appearanceUseSystemSettingChecked)
            {
                return;
            }

            _appearanceUseSystemSettingChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceUseSystemSettingChecked)));

            if (value)
            {
                SetAppearance(AppSettingsModel.AppearanceSetting.UseSystemSetting);
            }
        }
    }

    private bool _appearanceChanged;
    public bool AppearanceChanged
    {
        get => _appearanceChanged;
        set
        {
            _appearanceChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceChanged)));
        }
    }

    private string _statisticText = string.Empty;
    public string StatisticText
    {
        get => _statisticText;
        set
        {
            _statisticText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatisticText)));
        }
    }

    private string _languageDescription = "";
    public string LanguageDescription
    {
        get => _languageDescription;
        set
        {
            _languageDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageDescription)));
        }
    }

    private bool _debugMode;
    public bool DebugMode
    {
        get => _debugMode;
        set
        {
            _debugMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugMode)));
        }
    }

    private bool _isRescanning = true;
    public bool IsRescanning
    {
        get => _isRescanning;
        set
        {
            _isRescanning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRescanning)));
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
        get => StringResourceProvider.Instance.ClearCacheDetail.Replace("$size", _cacheSize);
        set
        {
            _cacheSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CacheSize)));
        }
    }

    private bool _isDonor = false;
    public bool IsDonor
    {
        get => _isDonor;
        set
        {
            _isDonor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDonor)));
        }
    }

    public void Initialize(ILifecycleOwner owner)
    {
        _dispatcher.Submit($"{TAG}#Initialize", InitializeInternal);

        GlobalEvent.Instance.ComicUpdated.Observe(owner, (_) =>
        {
            _dispatcher.Submit($"{TAG}#UpdateStatistis", () =>
            {
                UpdateStatistis();
            });
        });

        ComicHandle.IsScanningLibrary.ObserveSticky(owner, (bool isScanning) =>
        {
            IsRescanning = isScanning;
        });
    }

    public void SetScanOnLaunch(bool scanOnLaunch)
    {
        _scanOnLaunch = scanOnLaunch;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.ScanOnLaunch = scanOnLaunch;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetRemoveUnreachableComics(bool removeUnreachableComics)
    {
        _removeUnreachableComics = removeUnreachableComics;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.RemoveUnreachableComics = removeUnreachableComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetRestoreLastReadingPosition(bool restoreLastReadingPosition)
    {
        _restoreLastReadingPosition = restoreLastReadingPosition;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.RestoreLastReadingPosition = restoreLastReadingPosition;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetPromptBeforeRemovingComics(bool promptBeforeRemovingComics)
    {
        _promptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.PromptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void RefreshRandomSeed()
    {
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.ComicShuffleRandomSeed = Random.Shared.Next();
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetBackground(int index)
    {
        if (index == _backgroundIndex)
        {
            return;
        }

        if (index >= _backgrounds.Count)
        {
            Logger.F(TAG, "Background index out of bounds.");
            return;
        }

        AppearanceChanged = true;
        BackgroundEntry selectedBackground = _backgrounds[index];
        _backgroundIndex = index;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.Background = selectedBackground.Value;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetAppLanguage(int index)
    {
        if (index == _languageIndex)
        {
            return;
        }

        if (index >= _languages.Count)
        {
            Logger.F(TAG, "Language index out of bounds.");
            return;
        }

        LanguageEntry selectedLanguage = _languages[index];
        _languageIndex = index;
        _languageChanged = true;
        UpdateLanguageDescription(selectedLanguage.Description);

        if (!EnvironmentProvider.IsPortable())
        {
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = selectedLanguage.Identifier;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
            }
        }

        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.Language = selectedLanguage.Identifier;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetAppearance(AppSettingsModel.AppearanceSetting appearance)
    {
        AppearanceChanged = true;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.Theme = appearance;
        AppSettingsModel.Instance.UpdateModel(model);
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

    //
    // Initialization
    //

    private void InitializeInternal()
    {
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        UpdateEncodings();
        UpdateReaderSettings();
        UpdateHistory(model);
        UpdateAppearance(model);
        UpdateBackground(model);
        UpdateLanguage(model);
        UpdateStatistis();
        UpdateCacheSize();
        UpdateOtherSettings();
    }

    private void UpdateEncodings()
    {
        ReadOnlyDictionary<int, Encoding> supportedEncodings = AppInfoProvider.GetSupportedEncodings();
        var encodings = new List<Tuple<string, int>>
        {
            new(StringResourceProvider.Instance.Default, -1)
        };
        int defaultCodePage = AppModel.DefaultArchiveCodePage;
        int selectedIndex = 0;
        foreach (Encoding info in supportedEncodings.Values)
        {
            string title = info.EncodingName + " [" + info.CodePage.ToString() + "]";
            encodings.Add(new Tuple<string, int>(title, info.CodePage));
            if (defaultCodePage == info.CodePage)
            {
                selectedIndex = encodings.Count - 1;
            }
        }
        if (!supportedEncodings.ContainsKey(defaultCodePage))
        {
            AppModel.DefaultArchiveCodePage = -1;
            selectedIndex = 0;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            Encodings = encodings;
            DefaultArchiveCodePageIndex = selectedIndex;
        });
    }

    private void UpdateReaderSettings()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            TransitionAnimation = AppModel.TransitionAnimation;
            AntiAliasingEnabled = AppModel.AntiAliasingEnabled;
            AutomaticallyHideCursor = AppModel.AutomaticallyHideCursor;
        });
    }

    private void UpdateHistory(AppSettingsModel.ExternalModel model)
    {
        CoroutineUtils.Start(async () =>
        {
            bool hasHistory = !await ComicHistoryItemModel.IsEmptyAsync();
            bool scanOnLaunch = model.ScanOnLaunch;
            bool removeUnreachableComics = model.RemoveUnreachableComics;
            bool promptBeforeRemovingComics = model.PromptBeforeRemovingComics;
            bool restoreLastReadingPosition = model.RestoreLastReadingPosition;
            bool saveBrowsingHistory = AppModel.SaveBrowsingHistory;

            await MainThreadUtils.RunInMainThread(() =>
            {
                IsClearHistoryEnabled = hasHistory;
                ScanOnLaunch = scanOnLaunch;
                RemoveUnreachableComics = removeUnreachableComics;
                PromptBeforeRemovingComics = promptBeforeRemovingComics;
                HistorySaveBrowsingHistory = saveBrowsingHistory;
                RestoreLastReadingPosition = restoreLastReadingPosition;
            });
        });
    }

    private void UpdateBackground(AppSettingsModel.ExternalModel model)
    {
        AppSettingsModel.AppBackgroundEnum background = model.Background;
        List<BackgroundEntry> backgrounds = [
            new(StringResourceProvider.Instance.None, AppSettingsModel.AppBackgroundEnum.None),
            new(StringResourceProvider.Instance.BackgroundAcrylic, AppSettingsModel.AppBackgroundEnum.Acrylic)
        ];
        int backgroundIndex = backgrounds.FindIndex(x => x.Value == background);
        if (backgroundIndex < 0)
        {
            backgroundIndex = 0;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            Backgrounds = backgrounds;
            BackgroundIndex = backgroundIndex;
        });
    }

    private void UpdateLanguage(AppSettingsModel.ExternalModel model)
    {
        string currentLanguage = model.Language;
        List<LanguageEntry> languages = [
            new("Deutsch", "de-DE", "Einige Texte sind maschinell übersetzt"),
            new("Español", "es-ES", "Algunos textos están traducidos automáticamente"),
            new("Français", "fr-FR", "Certains textes sont traduits automatiquement"),
            new("English", "en", ""),
            new("日本語", "ja-JP", "一部のテキストは機械翻訳されています"),
            new("한국어", "ko-KR", "일부 텍스트는 기계로 번역되었습니다"),
            new("Русский", "ru-RU", "Некоторые тексты переведены машинным способом"),
            new("简体中文", "zh-CN", ""),
            new("繁體中文", "zh-TW", "部分文字使用了機器翻譯"),
        ];
        languages.Sort((x, y) => x.Identifier.CompareTo(y.Identifier));
        LanguageEntry useSystemLanguage = new(StringResourceProvider.Instance.UseSystemLanguage, "", GetLanguageDescriptionOfSystemLanguage(languages));
        languages.Insert(0, useSystemLanguage);
        int selectedIndex = -1;
        string languageDescription = "";
        for (int i = 0; i < languages.Count; i++)
        {
            if (currentLanguage == languages[i].Identifier)
            {
                selectedIndex = i;
                languageDescription = languages[i].Description;
                break;
            }
        }
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            Languages = languages;
            LanguageIndex = selectedIndex;
            UpdateLanguageDescription(languageDescription);
        });
    }

    private void UpdateLanguageDescription(string description)
    {
        if (_languageChanged)
        {
            if (description.Length == 0)
            {
                description = StringResourceProvider.Instance.ApplyOnNextLaunch;
            }
            else
            {
                description += "\n" + StringResourceProvider.Instance.ApplyOnNextLaunch;
            }
        }
        LanguageDescription = description;
    }

    private void UpdateAppearance(AppSettingsModel.ExternalModel model)
    {
        AppSettingsModel.AppearanceSetting appearance = model.Theme;
        if (!Enum.IsDefined(appearance))
        {
            appearance = AppSettingsModel.AppearanceSetting.UseSystemSetting;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            // Reset first to avoid triggering the change event
            _appearanceLightChecked = appearance == AppSettingsModel.AppearanceSetting.Light;
            AppearanceLightChecked = _appearanceLightChecked;
            _appearanceDarkChecked = appearance == AppSettingsModel.AppearanceSetting.Dark;
            AppearanceDarkChecked = _appearanceDarkChecked;
            _appearanceUseSystemSettingChecked = appearance == AppSettingsModel.AppearanceSetting.UseSystemSetting;
            AppearanceUseSystemSettingChecked = _appearanceUseSystemSettingChecked;
        });
    }

    private void UpdateStatistis()
    {
        long QueryComicCount(Action<SelectCommand>? condition = null)
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            condition?.Invoke(command);
            IReaderToken<long> comicCountToken = command.PutQueryCountAll();
            using SelectCommand.IReader reader = command.Execute();
            long result = 0;
            if (reader.Read())
            {
                result = comicCountToken.GetValue();
            }
            return result;
        }

        long comicCount = 0;
        long unreadComicCount = 0;
        long readingComicCount = 0;
        long finishedComicCount = 0;
        ComicHandle.Enqueue("SettingPage#UpdateStatistis", () =>
        {
            comicCount = QueryComicCount();
            unreadComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.NotStarted));
            readingComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.Started));
            finishedComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.Completed));
            return true;
        }).Wait();

        string textWithColon = StringResourceProvider.Instance.TextWithColon;
        StringBuilder sb = new();
        sb.Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.TotalComics)).Append(comicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.CompletionStatusUnread)).Append(unreadComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.CompletionStatusReading)).Append(readingComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.CompletionStatusFinished)).Append(finishedComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        string statisticText = sb.ToString();

        CoroutineUtils.RunInMainThread(() =>
        {
            StatisticText = statisticText;
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

    public void UpdateOtherSettings()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            DebugMode = DebugUtils.DebugMode;
        });
    }

    //
    // Data persistence
    //

    private AppSettingsModel.ExternalModel GetSettingsModel()
    {
        _lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            AppSettingsModel.ExternalModel? model = _settingsModel;
            if (model is not null)
            {
                return model;
            }

            LockCookie cookie = _lock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                model = AppSettingsModel.Instance.GetModel();
                _settingsModel = model;
                return model;
            }
            finally
            {
                _lock.DowngradeFromWriterLock(ref cookie);
            }
        }
        finally
        {
            _lock.ReleaseReaderLock();
        }
    }

    //
    // File cache
    //

    private static string GetCacheSize()
    {
        long size = 0;
        var localCacheDir = new DirectoryInfo(StorageLocation.LocalCacheFolderPath);
        size += GetCacheDirectorySize(localCacheDir);
        var tempDir = new DirectoryInfo(StorageLocation.TemporaryFolderPath);
        size += GetCacheDirectorySize(tempDir);

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format("{0:0.##} {1}", size, sizes[order]);
    }

    private static void ClearCacheInternal()
    {
        ImageCacheManager.Clear();
        DirectoryInfo cacheDir = new(StorageLocation.LocalCacheFolderPath);
        ClearCacheDirectory(cacheDir);
        DirectoryInfo tempDir = new(StorageLocation.TemporaryFolderPath);
        ClearCacheDirectory(tempDir);
    }

    private static long GetCacheDirectorySize(DirectoryInfo directory)
    {
        long size = 0;

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

    private static void ClearCacheDirectory(DirectoryInfo directory)
    {
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
            catch (IOException e)
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
            catch (IOException e)
            {
                Logger.E(TAG, "ClearDirectory", e);
            }
        }
    }

    //
    // Language
    //

    private static string GetLanguageDescriptionOfSystemLanguage(List<LanguageEntry> entries)
    {
        string systemLanguage = EnvironmentProvider.GetCurrentSystemLanguage();
        foreach (LanguageEntry entry in entries)
        {
            if (entry.Identifier == systemLanguage)
            {
                return entry.Description;
            }
        }
        systemLanguage = systemLanguage.Split('-')[0];
        foreach (LanguageEntry entry in entries)
        {
            string neutralTag = entry.Identifier.Split('-')[0];
            if (neutralTag == systemLanguage)
            {
                return entry.Description;
            }
        }
        return "";
    }

    //
    // Types
    //

    public class BackgroundEntry(string name, AppSettingsModel.AppBackgroundEnum value)
    {
        public string Name { get; set; } = name;
        public AppSettingsModel.AppBackgroundEnum Value { get; set; } = value;
    }

    public class LanguageEntry(string name, string identifier, string description)
    {
        public string Name { get; set; } = name;
        public string Identifier { get; set; } = identifier;
        public string Description { get; set; } = description;
    }
}
