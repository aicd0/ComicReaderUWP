// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Windows.Foundation;

namespace ComicReaderUWP.Common.Models.F8;

internal struct RectF8 : IFormattable
{
    public double _x;

    public double _y;

    public double _width;

    public double _height;

    private const double EmptyX = double.PositiveInfinity;

    private const double EmptyY = double.PositiveInfinity;

    private const double EmptyWidth = double.NegativeInfinity;

    private const double EmptyHeight = double.NegativeInfinity;

    private static readonly RectF8 s_empty = CreateEmptyRect();

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

    public readonly double Right
    {
        get
        {
            if (IsEmpty)
            {
                return double.NegativeInfinity;
            }

            return _x + _width;
        }
    }

    public readonly double Bottom
    {
        get
        {
            if (IsEmpty)
            {
                return double.NegativeInfinity;
            }

            return _y + _height;
        }
    }

    public static RectF8 Empty => s_empty;

    public readonly bool IsEmpty => _width < 0.0;

    public RectF8(double x, double y, double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0f);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0f);
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public RectF8(Point point1, Point point2)
    {
        _x = Math.Min(point1._x, point2._x);
        _y = Math.Min(point1._y, point2._y);
        _width = Math.Max(Math.Max(point1._x, point2._x) - _x, 0f);
        _height = Math.Max(Math.Max(point1._y, point2._y) - _y, 0f);
    }

    public RectF8(Point location, Size size)
    {
        if (size.IsEmpty)
        {
            this = s_empty;
            return;
        }

        _x = location._x;
        _y = location._y;
        _width = size._width;
        _height = size._height;
    }

    public readonly bool Contains(Point point)
    {
        return ContainsInternal(point._x, point._y);
    }

    public void Intersect(RectF8 rect)
    {
        if (!IntersectsWith(rect))
        {
            this = s_empty;
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

    public void Union(Point point)
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

    private static RectF8 CreateEmptyRect()
    {
        return new RectF8
        {
            _x = double.PositiveInfinity,
            _y = double.PositiveInfinity,
            _width = double.NegativeInfinity,
            _height = double.NegativeInfinity
        };
    }

    public override readonly string ToString()
    {
        return ConvertToString(null, null);
    }

    public readonly string ToString(IFormatProvider provider)
    {
        return ConvertToString(null, provider);
    }

    readonly string IFormattable.ToString(string? format, IFormatProvider? provider)
    {
        return ConvertToString(format, provider);
    }

    internal readonly string ConvertToString(string? format, IFormatProvider? provider)
    {
        if (IsEmpty)
        {
            return "Empty.";
        }

        char numericListSeparator = TokenizerHelper.GetNumericListSeparator(provider);
        return string.Format(provider, "{1:" + format + "}{0}{2:" + format + "}{0}{3:" + format + "}{0}{4:" + format + "}", numericListSeparator, _x, _y, _width, _height);
    }

    public readonly bool Equals(RectF8 value)
    {
        return this == value;
    }

    public static bool operator ==(RectF8 rect1, RectF8 rect2)
    {
        if (rect1._x == rect2._x && rect1._y == rect2._y && rect1._width == rect2._width)
        {
            return rect1._height == rect2._height;
        }

        return false;
    }

    public static bool operator !=(RectF8 rect1, RectF8 rect2)
    {
        return !(rect1 == rect2);
    }

    public override readonly bool Equals(object? o)
    {
        if (o is RectF8 rect)
        {
            return this == rect;
        }

        return false;
    }

    public override readonly int GetHashCode()
    {
        return X.GetHashCode() ^ Y.GetHashCode() ^ Width.GetHashCode() ^ Height.GetHashCode();
    }
}
