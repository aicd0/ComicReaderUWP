// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common;

namespace ComicReader.Views.Pages.Navigation;

internal partial class SidePaneViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _pinButtonGlyph = string.Empty;
    public string PinButtonGlyph
    {
        get => _pinButtonGlyph;
        set
        {
            _pinButtonGlyph = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinButtonGlyph)));
        }
    }

    private string _pinButtonText = string.Empty;
    public string PinButtonText
    {
        get => _pinButtonText;
        set
        {
            _pinButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinButtonText)));
        }
    }

    public void UpdatePinButton(bool pinned)
    {
        if (pinned)
        {
            PinButtonGlyph = "\uE77A";
            PinButtonText = StringResourceProvider.Instance.Unpin;
        }
        else
        {
            PinButtonGlyph = "\uE718";
            PinButtonText = StringResourceProvider.Instance.Pin;
        }
    }
}
