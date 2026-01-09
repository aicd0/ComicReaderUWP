// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReaderUWP.Common.Expression.Filter.Tokens;

class FinalFuncTokenExtra : ITokenExtra
{
    public readonly string Name;
    public readonly IReadOnlyList<ExpressionToken> Parameters;

    public FinalFuncTokenExtra(string name, IEnumerable<ExpressionToken> parameters)
    {
        foreach (ExpressionToken parameter in parameters)
        {
            if (parameter.Level != ExpressionToken.LEVEL_FINAL)
            {
                throw new ExpressionException("All parameters must be final tokens.");
            }
        }

        Name = name;
        Parameters = [.. parameters];
    }

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Name).Append('(');
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (i > 0)
            {
                stringBuilder.Append(", ");
            }
            Parameters[i].Extra?.ToString(stringBuilder);
        }
        stringBuilder.Append(')');
    }
}
