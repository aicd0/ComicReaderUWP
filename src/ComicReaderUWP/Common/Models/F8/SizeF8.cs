// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReaderUWP.Common.Models.F8;

internal struct SizeF8
{
    public double _width;

    public double _height;

    private static readonly SizeF8 s_empty = CreateEmptySizeF8();

    public double Width
    {
        readonly get
        {
            return _width;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0.0);
            _width = value;
        }
    }

    public double Height
    {
        readonly get
        {
            return _height;
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0.0);
            _height = value;
        }
    }

    public static SizeF8 Empty => s_empty;

    public readonly bool IsEmpty => Width < 0.0;

    public SizeF8(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0.0);
        _width = width;
        _height = height;
    }

    private static SizeF8 CreateEmptySizeF8()
    {
        return new SizeF8
        {
            _width = double.NegativeInfinity,
            _height = double.NegativeInfinity
        };
    }

    public static bool operator ==(SizeF8 size1, SizeF8 size2)
    {
        if (size1._width == size2._width)
        {
            return size1._height == size2._height;
        }

        return false;
    }

    public static bool operator !=(SizeF8 size1, SizeF8 size2)
    {
        return !(size1 == size2);
    }

    public override readonly bool Equals(object? o)
    {
        if (o is SizeF8 size)
        {
            return Equals(this, size);
        }

        return false;
    }

    public readonly bool Equals(SizeF8 value)
    {
        return Equals(this, value);
    }

    public override readonly int GetHashCode()
    {
        if (IsEmpty)
        {
            return 0;
        }

        return Width.GetHashCode() ^ Height.GetHashCode();
    }

    private static bool Equals(SizeF8 size1, SizeF8 size2)
    {
        if (size1.IsEmpty)
        {
            return size2.IsEmpty;
        }

        if (size1._width.Equals(size2._width))
        {
            return size1._height.Equals(size2._height);
        }

        return false;
    }

    public override readonly string ToString()
    {
        if (IsEmpty)
        {
            return "Empty";
        }

        return $"{_width},{_height}";
    }
}
