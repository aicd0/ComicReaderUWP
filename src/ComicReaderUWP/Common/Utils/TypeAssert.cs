// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReaderUWP.Common.Utils;

internal static class TypeAssert
{
    public static bool AssertBoolean(bool value) { return value; }

    [Obsolete("", error: true)]
    public static bool AssertBoolean<T>(T _) { return false; }

    public static int AssertInt(int value) { return value; }

    [Obsolete("", error: true)]
    public static int AssertInt<T>(T _) { return 0; }

    public static long AssertLong(long value) { return value; }

    [Obsolete("", error: true)]
    public static long AssertLong<T>(T _) { return 0L; }

    public static float AssertFloat(float value) { return value; }

    [Obsolete("", error: true)]
    public static float AssertFloat<T>(T _) { return 0F; }

    public static double AssertDouble(double value) { return value; }

    [Obsolete("", error: true)]
    public static double AssertDouble<T>(T _) { return 0.0; }

    public static string AssertString(string value) { return value; }

    [Obsolete("", error: true)]
    public static string AssertString<T>(T _) { return string.Empty; }

    public static DateTimeOffset AssertDateTimeOffset(DateTimeOffset value) { return value; }

    [Obsolete("", error: true)]
    public static DateTimeOffset AssertDateTimeOffset<T>(T _) { return DateTimeOffset.MinValue; }
}
