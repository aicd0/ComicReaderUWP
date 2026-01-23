// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.SDK.Common.AppEnvironment;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.SqlHelpers;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class SettingsPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(SettingsPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsSharedViewModel Shared { get; } = new();

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
                AppSettingsModel.Instance.DefaultArchiveCodePage = Encodings[selectedIndex].Item2;
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

            AppSettingsModel.Instance.SaveBrowsingHistory = value;
        }
    }

    private int _appearanceIndex;
    public int AppearanceIndex
    {
        get => _appearanceIndex;
        set
        {
            _appearanceIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceIndex)));
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

    private bool _languageChanged;
    public bool LanguageChanged
    {
        get => _languageChanged;
        set
        {
            _languageChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageDescription)));
        }
    }

    private string _languageDescription = string.Empty;
    public string LanguageDescription
    {
        get
        {
            List<string> lines = [];

            if (_languageChanged)
            {
                lines.Add(StringResourceProvider.Instance.ApplyOnNextLaunch);
            }

            if (!string.IsNullOrEmpty(_languageDescription))
            {
                lines.Add(_languageDescription);
            }

            return string.Join('\n', lines);
        }
        set
        {
            _languageDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageDescription)));
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

    public bool IsPortable => EnvironmentProvider.IsPortable();

    private readonly ReaderWriterLock _lock = new();
    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.DefaultQueue;
    private AppSettingsModel.ExternalModel? _settingsModel;

    public void Initialize(ILifecycleOwner owner)
    {
        GlobalEvent.Instance.ComicUpdated.Observe(owner, (_) =>
        {
            _dispatcher.Submit($"{TAG}#UpdateStatistis", () =>
            {
                UpdateStatistis();
            });
        });

        ComicHandle.IsScanningLibrary.ObserveSticky(owner, isScanning =>
        {
            IsRescanning = isScanning;
        });

        Shared.UpdateStarted += Update;
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

    public void SetPromptBeforeRemovingComics(bool promptBeforeRemovingComics)
    {
        _promptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.PromptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetBackground(int index)
    {
        if (index == _backgroundIndex)
        {
            return;
        }

        if (index < 0 || index >= _backgrounds.Count)
        {
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
        if (index < 0 || index >= _languages.Count || index == _languageIndex)
        {
            return;
        }

        LanguageEntry selectedLanguage = _languages[index];
        _languageIndex = index;
        LanguageChanged = true;
        LanguageDescription = selectedLanguage.Description;
        AppSettingsModel.Instance.Language = selectedLanguage.Identifier;
    }

    public void SetAppearance(int index)
    {
        if (index == _appearanceIndex)
        {
            return;
        }

        _appearanceIndex = index;
        AppSettingsModel.AppearanceSetting appearance = index switch
        {
            0 => AppSettingsModel.AppearanceSetting.Light,
            1 => AppSettingsModel.AppearanceSetting.Dark,
            2 => AppSettingsModel.AppearanceSetting.UseSystemSetting,
            _ => AppSettingsModel.AppearanceSetting.UseSystemSetting,
        };
        AppearanceChanged = true;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        model.Theme = appearance;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    //
    // Initialization
    //

    private void Update()
    {
        _dispatcher.Submit($"{TAG}#Initialize", InitializeInternal);
    }

    private void InitializeInternal()
    {
        _settingsModel = null;
        AppSettingsModel.ExternalModel model = GetSettingsModel();
        UpdateEncodings();
        UpdateHistory(model);
        UpdateAppearance(model);
        UpdateBackground(model);
        UpdateLanguage();
        UpdateStatistis();
        UpdateSharedSettings();

        CoroutineUtils.RunInMainThread(() =>
        {
            AppearanceChanged = false;
            LanguageChanged = false;
        });
    }

    private void UpdateEncodings()
    {
        ReadOnlyDictionary<int, Encoding> supportedEncodings = AppInfoProvider.GetSupportedEncodings();
        var encodings = new List<Tuple<string, int>>
        {
            new(StringResourceProvider.Instance.Default, -1)
        };
        int defaultCodePage = AppSettingsModel.Instance.DefaultArchiveCodePage;
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
            AppSettingsModel.Instance.DefaultArchiveCodePage = -1;
            selectedIndex = 0;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            Encodings = encodings;
            DefaultArchiveCodePageIndex = selectedIndex;
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
            bool saveBrowsingHistory = AppSettingsModel.Instance.SaveBrowsingHistory;

            await MainThreadUtils.RunInMainThread(() =>
            {
                IsClearHistoryEnabled = hasHistory;
                ScanOnLaunch = scanOnLaunch;
                RemoveUnreachableComics = removeUnreachableComics;
                PromptBeforeRemovingComics = promptBeforeRemovingComics;
                HistorySaveBrowsingHistory = saveBrowsingHistory;
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

    private void UpdateLanguage()
    {
        string currentLanguage = AppSettingsModel.Instance.Language;
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
            LanguageDescription = languageDescription;
        });
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
            AppearanceIndex = appearance switch
            {
                AppSettingsModel.AppearanceSetting.Light => 0,
                AppSettingsModel.AppearanceSetting.Dark => 1,
                AppSettingsModel.AppearanceSetting.UseSystemSetting => 2,
                _ => 2,
            };
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

    private void UpdateSharedSettings()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            Shared.DebugMode = DebugUtils.DebugMode;
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
