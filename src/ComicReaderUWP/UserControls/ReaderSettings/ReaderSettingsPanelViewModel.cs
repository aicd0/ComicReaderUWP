// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal partial class ReaderSettingsPanelViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly MutableLiveData<bool> _settingsChangedLiveData = new();
    public readonly MutableLiveDataWithDelay<bool> SettingsChangedLiveData;

    private string _generalTabTitle = string.Empty;
    public string GeneralTabTitle
    {
        get => _generalTabTitle;
        set
        {
            _generalTabTitle = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GeneralTabTitle)));
        }
    }

    private string _imageProcessingTabTitle = string.Empty;
    public string ImageProcessingTabTitle
    {
        get => _imageProcessingTabTitle;
        set
        {
            _imageProcessingTabTitle = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageProcessingTabTitle)));
        }
    }

    private string _twoPageModeLabel = string.Empty;
    public string TwoPageModeLabel
    {
        get => _twoPageModeLabel;
        set
        {
            _twoPageModeLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TwoPageModeLabel)));
        }
    }

    private string _enableCoverLabel = string.Empty;
    public string EnableCoverLabel
    {
        get => _enableCoverLabel;
        set
        {
            _enableCoverLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableCoverLabel)));
        }
    }

    private string _swapLeftAndRightPagesLabel = string.Empty;
    public string SwapLeftAndRightPagesLabel
    {
        get => _swapLeftAndRightPagesLabel;
        set
        {
            _swapLeftAndRightPagesLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwapLeftAndRightPagesLabel)));
        }
    }

    private string _spreadDetectionLabel = string.Empty;
    public string SpreadDetectionLabel
    {
        get => _spreadDetectionLabel;
        set
        {
            _spreadDetectionLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpreadDetectionLabel)));
        }
    }

    private string _pageSpacingLabel = string.Empty;
    public string PageSpacingLabel
    {
        get => _pageSpacingLabel;
        set
        {
            _pageSpacingLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageSpacingLabel)));
        }
    }

    private string _autoScrollingLabel = string.Empty;
    public string AutoScrollingLabel
    {
        get => _autoScrollingLabel;
        set
        {
            _autoScrollingLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoScrollingLabel)));
        }
    }

    private string _originalSizeLabel = string.Empty;
    public string OriginalSizeLabel
    {
        get => _originalSizeLabel;
        set
        {
            _originalSizeLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OriginalSizeLabel)));
        }
    }

    private string _flipImageLabel = string.Empty;
    public string FlipImageLabel
    {
        get => _flipImageLabel;
        set
        {
            _flipImageLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlipImageLabel)));
        }
    }

    private string _invertImageLabel = string.Empty;
    public string InvertImageLabel
    {
        get => _invertImageLabel;
        set
        {
            _invertImageLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InvertImageLabel)));
        }
    }

    private string _antiAliasingFilterLabel = string.Empty;
    public string AntiAliasingFilterLabel
    {
        get => _antiAliasingFilterLabel;
        set
        {
            _antiAliasingFilterLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AntiAliasingFilterLabel)));
        }
    }

    private string _rotationLabel = string.Empty;
    public string RotationLabel
    {
        get => _rotationLabel;
        set
        {
            _rotationLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RotationLabel)));
        }
    }

    public ReaderSettingsPanelViewModel()
    {
        SettingsChangedLiveData = new(_settingsChangedLiveData, 500);
    }
}
