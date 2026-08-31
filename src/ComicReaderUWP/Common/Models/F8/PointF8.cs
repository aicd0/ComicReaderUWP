// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.Common.Models.F8;

internal struct PointF8 : IEquatable<PointF8>
{
    public static readonly PointF8 Empty = new();

    private double _x;
    private double _y;

    public PointF8(double x, double y)
    {
        _x = x;
        _y = y;
    }

    public readonly bool IsEmpty => _x == 0.0 && _y == 0.0;

    public double X
    {
        readonly get => _x;
        set => _x = value;
    }

    public double Y
    {
        readonly get => _y;
        set => _y = value;
    }

    public static PointF8 operator +(PointF8 pt, SizeF8 sz) => Add(pt, sz);

    public static PointF8 operator -(PointF8 pt, SizeF8 sz) => Subtract(pt, sz);

    public static bool operator ==(PointF8 left, PointF8 right) => left.X == right.X && left.Y == right.Y;

    public static bool operator !=(PointF8 left, PointF8 right) => !(left == right);

    public static PointF8 Add(PointF8 pt, SizeF8 sz) => new(pt.X + sz.Width, pt.Y + sz.Height);

    public static PointF8 Subtract(PointF8 pt, SizeF8 sz) => new(pt.X - sz.Width, pt.Y - sz.Height);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is PointF8 pt && Equals(pt);

    public readonly bool Equals(PointF8 other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Y.GetHashCode());

    public override readonly string ToString() => $"{{X={_x}, Y={_y}}}";
}