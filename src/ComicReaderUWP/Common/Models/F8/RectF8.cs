// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace ComicReaderUWP.Common.Models.F8;

internal struct RectF8 : IEquatable<RectF8>
{
    public static readonly RectF8 Empty = new();

    private double _x;
    private double _y;
    private double _width;
    private double _height;

    public RectF8(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0.0);
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public RectF8(PointF8 point1, PointF8 point2)
    {
        _x = Math.Min(point1.X, point2.X);
        _y = Math.Min(point1.Y, point2.Y);
        _width = Math.Max(Math.Max(point1.X, point2.X) - _x, 0);
        _height = Math.Max(Math.Max(point1.Y, point2.Y) - _y, 0);
    }

    public RectF8(PointF8 location, SizeF8 size)
    {
        if (size.IsEmpty)
        {
            this = Empty;
            return;
        }

        _x = location.X;
        _y = location.Y;
        _width = size.Width;
        _height = size.Height;
    }

    public readonly bool IsEmpty => _x == 0.0 && _y == 0.0 && _width == 0.0 && _height == 0.0;

    public double X
    {
        readonly get
        {
            return _x;
        }
        set
        {
            _x = value;
        }
    }

    public double Y
    {
        readonly get
        {
            return _y;
        }
        set
        {
            _y = value;
        }
    }

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

    public readonly double Left => _x;

    public readonly double Top => _y;

    public readonly double Right => _x + _width;

    public readonly double Bottom => _y + _height;

    public readonly bool Contains(PointF8 point)
    {
        return ContainsInternal(point.X, point.Y);
    }

    public void Intersect(RectF8 rect)
    {
        if (!IntersectsWith(rect))
        {
            this = Empty;
            return;
        }

        double num = Math.Max(X, rect.X);
        double num2 = Math.Max(Y, rect.Y);
        Width = Math.Max(Math.Min(X + Width, rect.X + rect.Width) - num, 0.0);
        Height = Math.Max(Math.Min(Y + Height, rect.Y + rect.Height) - num2, 0.0);
        X = num;
        Y = num2;
    }

    public void Union(RectF8 rect)
    {
        if (IsEmpty)
        {
            this = rect;
        }
        else if (!rect.IsEmpty)
        {
            double num = Math.Min(Left, rect.Left);
            double num2 = Math.Min(Top, rect.Top);
            if (rect.Width == double.PositiveInfinity || Width == double.PositiveInfinity)
            {
                Width = double.PositiveInfinity;
            }
            else
            {
                double num3 = Math.Max(Right, rect.Right);
                Width = Math.Max(num3 - num, 0.0);
            }

            if (rect.Height == double.PositiveInfinity || Height == double.PositiveInfinity)
            {
                Height = double.PositiveInfinity;
            }
            else
            {
                double num4 = Math.Max(Bottom, rect.Bottom);
                Height = Math.Max(num4 - num2, 0.0);
            }

            X = num;
            Y = num2;
        }
    }

    public void Union(PointF8 point)
    {
        Union(new RectF8(point, point));
    }

    private readonly bool ContainsInternal(double x, double y)
    {
        if (x >= _x && x - _width <= _x && y >= _y)
        {
            return y - _height <= _y;
        }

        return false;
    }

    internal readonly bool IntersectsWith(RectF8 rect)
    {
        if (_width < 0f || rect._width < 0f)
        {
            return false;
        }

        if (rect._x <= _x + _width && rect._x + rect._width >= _x && rect._y <= _y + _height)
        {
            return rect._y + rect._height >= _y;
        }

        return false;
    }

    public static bool operator ==(RectF8 left, RectF8 right)
    {
        if (left._x == right._x && left._y == right._y && left._width == right._width)
        {
            return left._height == right._height;
        }

        return false;
    }

    public static bool operator !=(RectF8 left, RectF8 right) => !(left == right);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is RectF8 rect && Equals(rect);

    public readonly bool Equals(RectF8 other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Y.GetHashCode(), Width.GetHashCode(), Height.GetHashCode());

    public override readonly string ToString() => $"{{X={_x}, Y={_y}, Width={_width}, Height={_height}}}";

    public static explicit operator RectangleF(RectF8 rect) => new((float)rect.Left, (float)rect.Top, (float)rect.Width, (float)rect.Height);
}
