// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.Common.Expression.Search.Tokens;

class FinalValueTokenExtra(string value) : ITokenExtra
{
    public readonly string Value = value;

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Value);
    }
}
