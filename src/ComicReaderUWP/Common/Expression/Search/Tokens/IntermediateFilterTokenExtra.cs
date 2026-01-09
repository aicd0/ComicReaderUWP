// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Common.Expression.Search.Tokens;

internal class IntermediateFilterTokenExtra(string key, string value) : ITokenExtra
{
    public readonly string Key = key;
    public readonly string Value = value;

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Key).Append(':').Append(Value);
    }
}
