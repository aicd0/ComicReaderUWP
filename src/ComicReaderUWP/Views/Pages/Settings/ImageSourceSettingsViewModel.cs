// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class ImageSourceSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(ImageSourceSettingsViewModel);

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
        }
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Shared.UpdateStarted += Update;
    }

    public void SetScanOnLaunch(bool scanOnLaunch)
    {
        _scanOnLaunch = scanOnLaunch;
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        model.ScanOnLaunch = scanOnLaunch;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetRemoveUnreachableComics(bool removeUnreachableComics)
    {
        _removeUnreachableComics = removeUnreachableComics;
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        model.RemoveUnreachableComics = removeUnreachableComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetPromptBeforeRemovingComics(bool promptBeforeRemovingComics)
    {
        _promptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        model.PromptBeforeRemovingComics = promptBeforeRemovingComics;
        AppSettingsModel.Instance.UpdateModel(model);
    }

    public void SetDefaultArchiveCodePage(int index)
    {
        if (index == _defaultArchiveCodePageIndex || index < 0 || index >= _encodings.Count)
        {
            return;
        }

        _defaultArchiveCodePageIndex = index;
        AppSettingsModel.Instance.DefaultArchiveCodePage = _encodings[index].Item2;
    }

    private void Update()
    {
        UpdateBasic();
        UpdateEncodings();
    }

    private void UpdateBasic()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.Instance.GetModel();
        bool scanOnLaunch = model.ScanOnLaunch;
        bool removeUnreachableComics = model.RemoveUnreachableComics;
        bool promptBeforeRemovingComics = model.PromptBeforeRemovingComics;

        ScanOnLaunch = scanOnLaunch;
        RemoveUnreachableComics = removeUnreachableComics;
        PromptBeforeRemovingComics = promptBeforeRemovingComics;
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

        Encodings = encodings;
        DefaultArchiveCodePageIndex = selectedIndex;
    }
}
