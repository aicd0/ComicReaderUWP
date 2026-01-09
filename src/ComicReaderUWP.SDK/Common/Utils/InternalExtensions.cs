// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.SDK.Common.Utils;

internal static class InternalExtensions
{
    public static void SafeAppend(this StringBuilder sb, string category, Func<object?> func)
    {
        string value;
        try
        {
            value = func()?.ToString() ?? "[null]";
        }
        catch (Exception)
        {
            return;
        }

        sb.Append(category);
        sb.Append(": ");
        sb.Append(value);
        sb.Append('\n');
    }
}
