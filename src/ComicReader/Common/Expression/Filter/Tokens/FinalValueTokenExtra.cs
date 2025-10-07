// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.Common.Expression.Filter.Tokens;

class FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum type, string value) : ITokenExtra
{
    public readonly TypeEnum Type = type;
    public readonly string Value = value;

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Value);
    }

    public enum TypeEnum
    {
        True,
        False,
        StringLiteral,
        NumberLiteralInteger,
        NumberLiteralDecimal,
    }
}
