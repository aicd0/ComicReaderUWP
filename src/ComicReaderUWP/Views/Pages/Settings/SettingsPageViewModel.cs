// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class SettingsPageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(SettingsPageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsSharedViewModel Shared { get; } = new();

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

    private string _aboutText = string.Empty;
    public string AboutText
    {
        get => _aboutText;
        set
        {
            _aboutText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AboutText)));
        }
    }

    private bool _languageChanged;
    public bool LanguageChanged
    {
        get => _languageChanged;
        set
        {
            _languageChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageChanged)));
        }
    }

    public void Initialize()
    {
        Shared.UpdateStarted += Update;
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
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
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
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        model.Theme = appearance;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void UpdateStatistics()
    {
        CoroutineUtils.Run(UpdateStatisticsInternal);
    }

    //
    // Initialization
    //

    private void Update()
    {
        UpdateAppearance();
        UpdateBackground();
        UpdateLanguage();
        CoroutineUtils.Run(UpdateStatisticsInternal);
        UpdateSharedSettings();

        AppearanceChanged = false;
        LanguageChanged = false;
    }

    private void UpdateBackground()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();

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

        Backgrounds = backgrounds;
        BackgroundIndex = backgroundIndex;
    }

    private void UpdateLanguage()
    {
        List<LanguageEntry> languages = [
            new("Deutsch", "de-DE"),
            new("Español", "es-ES"),
            new("Français", "fr-FR"),
            new("English", "en"),
            new("日本語", "ja-JP"),
            new("한국어", "ko-KR"),
            new("Русский", "ru-RU"),
            new("简体中文", "zh-CN"),
            new("繁體中文", "zh-TW"),
        ];
        languages.Sort((x, y) => x.Identifier.CompareTo(y.Identifier));
        LanguageEntry useSystemLanguage = new(StringResourceProvider.Instance.UseSystemLanguage, string.Empty);
        languages.Insert(0, useSystemLanguage);

        string currentLanguage = AppSettingsModel.Instance.Language;
        int selectedIndex = -1;
        for (int i = 0; i < languages.Count; i++)
        {
            if (currentLanguage == languages[i].Identifier)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        Languages = languages;
        LanguageIndex = selectedIndex;
    }

    private void UpdateAppearance()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();

        AppSettingsModel.AppearanceSetting appearance = model.Theme;
        if (!Enum.IsDefined(appearance))
        {
            appearance = AppSettingsModel.AppearanceSetting.UseSystemSetting;
        }

        AppearanceIndex = appearance switch
        {
            AppSettingsModel.AppearanceSetting.Light => 0,
            AppSettingsModel.AppearanceSetting.Dark => 1,
            AppSettingsModel.AppearanceSetting.UseSystemSetting => 2,
            _ => 2,
        };
    }

    private async Task UpdateStatisticsInternal()
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

        long total = 0;
        long hiddenCount = 0;
        List<Tuple<CompletionStatusEnum, long>> statusCount = [];
        await ComicHandle.Enqueue(() =>
        {
            total = QueryComicCount();
            hiddenCount = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnHidden, true));

            foreach (CompletionStatusEnum status in CompletionStatusService.AllStatus)
            {
                long count = QueryComicCount(c => c.AppendCondition(ComicTable.ColumnCompletionStatus, (int)status));
                statusCount.Add(new(status, count));
            }
        });

        StringBuilder sb = new();
        sb.Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.TotalComics))
            .Append(total.ToString("#,#0", CultureInfo.InvariantCulture))
            .AppendLine()
            .Append(StringResourceProvider.Instance.WithColon(StringResourceProvider.Instance.Hidden))
            .Append(hiddenCount.ToString("#,#0", CultureInfo.InvariantCulture));

        sb.AppendLine();
        foreach (Tuple<CompletionStatusEnum, long> pair in statusCount)
        {
            CompletionStatusEnum status = pair.Item1;
            long count = pair.Item2;
            int percentage = (int)Math.Round(100.0 * count / Math.Max(1, total));
            sb.AppendLine()
                .Append(StringResourceProvider.Instance.WithColon(CompletionStatusService.EnumToString(status)))
                .Append(count.ToString("#,#0", CultureInfo.InvariantCulture))
                .Append(" (").Append(percentage).Append("%)");
        }

        string statisticText = sb.ToString();
        StatisticText = statisticText;
    }

    private void UpdateSharedSettings()
    {
        Shared.DebugMode = DebugUtils.DebugMode;
    }

    //
    // Types
    //

    public class BackgroundEntry(string name, AppSettingsModel.AppBackgroundEnum value)
    {
        public string Name { get; set; } = name;
        public AppSettingsModel.AppBackgroundEnum Value { get; set; } = value;
    }

    public class LanguageEntry(string name, string identifier)
    {
        public string Name { get; set; } = name;
        public string Identifier { get; set; } = identifier;
    }
}
