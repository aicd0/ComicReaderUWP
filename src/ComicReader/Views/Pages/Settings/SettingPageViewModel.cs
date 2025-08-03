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

using ComicReader.Common;
using ComicReader.Common.Threading;
using ComicReader.Common.Utils;
using ComicReader.Data;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Data.SqlHelpers;

using Microsoft.UI.Xaml;

using Windows.Globalization;

namespace ComicReader.Views.Pages.Settings;

public partial class SettingPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(SettingPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ReaderWriterLock _lock = new();
    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.DefaultQueue;
    private AppSettingsModel.ExternalModel? _settingsModel;
    private AppSettingsModel.AppearanceSetting _initialAppearance = AppSettingsModel.AppearanceSetting.None;
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

    public void Initialize()
    {
        _dispatcher.Submit($"{TAG}#Initialize", InitializeInternal);
    }

    public void OnPageResume(FrameworkElement owner)
    {
        ComicData.LibraryUpdated.Observe(owner, (_) =>
        {
            _dispatcher.Submit($"{TAG}#UpdateStatistis", () =>
            {
                UpdateStatistis();
            });
        });

        ComicData.IsScanningLibrary.ObserveSticky(owner, (bool isScanning) =>
        {
            IsRescanning = isScanning;
        });
    }

    public void SetRemoveUnreachableComics(bool removeUnreachableComics)
    {
        _removeUnreachableComics = removeUnreachableComics;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.RemoveUnreachableComics = removeUnreachableComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetAppLanguage(int index)
    {
        if (index == _languageIndex)
        {
            return;
        }
        if (index >= Languages.Count)
        {
            Logger.AssertNotReachHere("B1015E06897635CE");
            return;
        }
        LanguageEntry selectedLanguage = Languages[index];
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
        AppearanceChanged = appearance != _initialAppearance;

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
            _ = MainThreadUtils.RunInMainThread(() =>
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

        MainThreadUtils.RunInMainThread(() =>
        {
            Encodings = encodings;
            DefaultArchiveCodePageIndex = selectedIndex;
        });
    }

    private void UpdateReaderSettings()
    {
        MainThreadUtils.RunInMainThread(() =>
        {
            TransitionAnimation = AppModel.TransitionAnimation;
            AntiAliasingEnabled = AppModel.AntiAliasingEnabled;
        });
    }

    private void UpdateHistory(AppSettingsModel.ExternalModel model)
    {
        bool hasHistory = HistoryModel.Instance.GetModel().Items.Count > 0;
        bool removeUnreachableComics = model.RemoveUnreachableComics;
        bool saveBrowsingHistory = AppModel.SaveBrowsingHistory;

        MainThreadUtils.RunInMainThread(() =>
        {
            IsClearHistoryEnabled = hasHistory;
            RemoveUnreachableComics = removeUnreachableComics;
            HistorySaveBrowsingHistory = saveBrowsingHistory;
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

        MainThreadUtils.RunInMainThread(() =>
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

        MainThreadUtils.RunInMainThread(() =>
        {
            _initialAppearance = appearance;
            AppearanceLightChecked = appearance == AppSettingsModel.AppearanceSetting.Light;
            AppearanceDarkChecked = appearance == AppSettingsModel.AppearanceSetting.Dark;
            AppearanceUseSystemSettingChecked = appearance == AppSettingsModel.AppearanceSetting.UseSystemSetting;
        });
    }

    private void UpdateStatistis()
    {
        long QueryComicCount(Action<SelectCommand>? condition = null)
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            condition?.Invoke(command);
            IReaderToken<long> comicCountToken = command.PutQueryCountAll();
            using SelectCommand.IReader reader = command.Execute(SqlDatabaseManager.MainDatabase);
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
        ComicData.Enqueue("SettingPage#UpdateStatistis", () =>
        {
            comicCount = QueryComicCount();
            unreadComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.NotStarted));
            readingComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.Started));
            finishedComicCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionState, (int)ComicCompletionStatusEnum.Completed));
            return true;
        }).Wait();

        string textWithColon = StringResourceProvider.Instance.TextWithColon;
        StringBuilder sb = new();
        sb.Append(textWithColon.Replace("$text", StringResourceProvider.Instance.TotalComics)).Append(comicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(textWithColon.Replace("$text", StringResourceProvider.Instance.Unread)).Append(unreadComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(textWithColon.Replace("$text", StringResourceProvider.Instance.Reading)).Append(readingComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        sb.Append('\n');
        sb.Append(textWithColon.Replace("$text", StringResourceProvider.Instance.Finished)).Append(finishedComicCount.ToString("#,#0", CultureInfo.InvariantCulture));
        string statisticText = sb.ToString();

        MainThreadUtils.RunInMainThread(() =>
        {
            StatisticText = statisticText;
        });
    }

    private void UpdateCacheSize()
    {
        string size = GetCacheSize();
        _ = MainThreadUtils.RunInMainThread(() =>
        {
            CacheSize = size;
        });
    }

    public void UpdateOtherSettings()
    {
        MainThreadUtils.RunInMainThread(() =>
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
            if (model != null)
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
        var d = new DirectoryInfo(StorageLocation.LocalCacheFolderPath);
        long size = GetCacheSize(d);
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format("{0:0.##} {1}", size, sizes[order]);
    }

    public static long GetCacheSize(DirectoryInfo directory)
    {
        long size = 0;

        {
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
        }

        {
            DirectoryInfo[] subDirectories;
            try
            {
                subDirectories = directory.GetDirectories();
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetCacheSize", e);
                subDirectories = [];
            }

            foreach (DirectoryInfo subDirectory in subDirectories)
            {
                if (subDirectory.Name == "Local")
                {
                    continue;
                }

                size += FileUtils.GetDirectorySize(subDirectory, ignoreErrors: true);
            }
        }

        return size;
    }

    private static void ClearCacheInternal()
    {
        var cacheDir = new DirectoryInfo(StorageLocation.LocalCacheFolderPath);

        foreach (FileInfo file in cacheDir.GetFiles())
        {
            try
            {
                file.Delete();
            }
            catch (IOException e)
            {
                Logger.E(TAG, "ClearCache", e);
            }
        }

        foreach (DirectoryInfo dir in cacheDir.GetDirectories())
        {
            if (dir.Name == "Local")
            {
                continue;
            }

            try
            {
                dir.Delete(true);
            }
            catch (IOException e)
            {
                Logger.E(TAG, "ClearCache", e);
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

    public class LanguageEntry(string name, string identifier, string description)
    {
        public string Name { get; set; } = name;
        public string Identifier { get; set; } = identifier;
        public string Description { get; set; } = description;
    }
}
