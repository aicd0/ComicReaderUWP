// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReaderUWP.Common.Expression.Filter.Tokens;

internal class FinalListTokenExtra : ITokenExtra
{
    public readonly IReadOnlyList<ExpressionToken> Expressions;

    public FinalListTokenExtra(IEnumerable<ExpressionToken> expressions)
    {
        foreach (ExpressionToken expression in expressions)
        {
            if (expression.Level != ExpressionToken.LEVEL_FINAL)
            {
                throw new ExpressionException("All parameters must be final tokens.");
            }
        }

        Expressions = [.. expressions];
    }

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append('(');
        for (int i = 0; i < Expressions.Count; i++)
        {
            if (i > 0)
            {
                stringBuilder.Append(", ");
            }
            Expressions[i].Extra?.ToString(stringBuilder);
        }
        stringBuilder.Append(')');
    }
}
