// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Numerics;

namespace ComicReaderUWP.Common.Models.F8;

internal struct SizeF8 : IEquatable<SizeF8>
{
    public static readonly SizeF8 Empty = new();

    private double _width;
    private double _height;

    public SizeF8(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0.0);
        _width = width;
        _height = height;
    }

    public readonly bool IsEmpty => _width == 0.0 && _height == 0.0;

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

    public static bool operator ==(SizeF8 left, SizeF8 right) => left.Width == right.Width && left.Height == right.Height;

    public static bool operator !=(SizeF8 left, SizeF8 right) => !(left == right);

    public override readonly bool Equals(object? obj) => obj is SizeF8 sz && Equals(sz);

    public readonly bool Equals(SizeF8 sz) => this == sz;

    public override readonly int GetHashCode() => HashCode.Combine(Width.GetHashCode(), Height.GetHashCode());

    public override readonly string ToString() => $"{{W={_width}, H={_height}}}";

    public static explicit operator Vector2(SizeF8 sz) => new((float)sz.Width, (float)sz.Height);
}
