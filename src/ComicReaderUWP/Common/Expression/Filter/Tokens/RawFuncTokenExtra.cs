// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Common.Expression.Filter.Tokens;

class RawFuncTokenExtra(string name) : ITokenExtra
{
    public readonly string Name = name;

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Name);
    }
}
