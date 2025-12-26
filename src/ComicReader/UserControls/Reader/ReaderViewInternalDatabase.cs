// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.UserControls.Reader;

internal class ReaderViewInternalDatabase(ReaderView.IConfigurationDatabase db)
{
    private const string KEY_CENTER_INSIDE_ZOOMING = "CenterInsideZooming";
    private const string KEY_VERTICAL_ZOOMING = "VerticalZooming";
    private const string KEY_HORIZONTAL_ZOOMING = "HorizontalZooming";
    private const string KEY_AUTO_SCROLL_VELOCITY = "AutoScrollVelocity";

    private bool _initialized = false;

    private double? _centerInsideZooming = null;
    public double? CenterInsideZooming
    {
        get
        {
            Initialize();
            return _centerInsideZooming;
        }
        set
        {
            if (_centerInsideZooming == value)
            {
                return;
            }

            _centerInsideZooming = value;
            Write(KEY_CENTER_INSIDE_ZOOMING, _centerInsideZooming?.ToString() ?? "");
        }
    }

    private double? _fitWidthDualAwareZooming = null;
    public double? FitWidthDualAwareZooming
    {
        get
        {
            Initialize();
            return _fitWidthDualAwareZooming;
        }
        set
        {
            if (_fitWidthDualAwareZooming == value)
            {
                return;
            }

            _fitWidthDualAwareZooming = value;
            Write(KEY_VERTICAL_ZOOMING, _fitWidthDualAwareZooming?.ToString() ?? "");
        }
    }

    private double? _fitHeightZooming = null;
    public double? FitHeightZooming
    {
        get
        {
            Initialize();
            return _fitHeightZooming;
        }
        set
        {
            if (_fitHeightZooming == value)
            {
                return;
            }

            _fitHeightZooming = value;
            Write(KEY_HORIZONTAL_ZOOMING, _fitHeightZooming?.ToString() ?? "");
        }
    }

    private double? _autoScrollVelocity = null;
    public double? AutoScrollVelocity
    {
        get
        {
            Initialize();
            return _autoScrollVelocity;
        }
        set
        {
            if (_autoScrollVelocity == value)
            {
                return;
            }

            _autoScrollVelocity = value;
            Write(KEY_AUTO_SCROLL_VELOCITY, _autoScrollVelocity?.ToString() ?? "");
        }
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _centerInsideZooming = ParseDouble(Read(KEY_CENTER_INSIDE_ZOOMING));
        _fitWidthDualAwareZooming = ParseDouble(Read(KEY_VERTICAL_ZOOMING));
        _fitHeightZooming = ParseDouble(Read(KEY_HORIZONTAL_ZOOMING));
        _autoScrollVelocity = ParseDouble(Read(KEY_AUTO_SCROLL_VELOCITY));
    }

    private string? Read(string key)
    {
        return db.ReadConfiguration(key);
    }

    private void Write(string key, string value)
    {
        db.WriteConfiguration(key, value);
    }

    private static double? ParseDouble(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }

        if (double.TryParse(s, out double value))
        {
            return value;
        }

        return null;
    }
}
