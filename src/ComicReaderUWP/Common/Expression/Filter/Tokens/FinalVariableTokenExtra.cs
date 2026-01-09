// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReaderUWP.Common.Expression.Filter.Tokens;

class FinalVariableTokenExtra(IEnumerable<string> path) : ITokenExtra
{
    public readonly IReadOnlyList<string> Path = [.. path];

    public void ToString(StringBuilder stringBuilder)
    {
        for (int i = 0; i < Path.Count; i++)
        {
            if (i > 0)
            {
                stringBuilder.Append('.');
            }
            stringBuilder.Append(Path[i]);
        }
    }
}
