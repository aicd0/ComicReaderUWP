// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Expression.Filter.Tokens;

namespace ComicReaderUWP.Common.Expression.Filter.Sql;

internal class VariableOrValue
{
    public readonly IReadOnlyList<string>? Path;
    public readonly object Value;

    public VariableOrValue(ExpressionToken token)
    {
        switch (token.Type)
        {
            case ExpressionToken.TYPE_FINAL_VALUE:
                Path = null;
                switch (token.FinalValueExtra.Type)
                {
                    case FinalValueTokenExtra.TypeEnum.True:
                        Value = true;
                        break;
                    case FinalValueTokenExtra.TypeEnum.False:
                        Value = false;
                        break;
                    case FinalValueTokenExtra.TypeEnum.StringLiteral:
                        Value = token.FinalValueExtra.Value;
                        break;
                    case FinalValueTokenExtra.TypeEnum.NumberLiteralInteger:
                        {
                            if (long.TryParse(token.FinalValueExtra.Value, out long result))
                            {
                                Value = result;
                            }
                            else
                            {
                                throw new ExpressionException($"Unable to parse number '{token.FinalValueExtra.Value}'");
                            }
                        }
                        break;
                    case FinalValueTokenExtra.TypeEnum.NumberLiteralDecimal:
                        {
                            if (double.TryParse(token.FinalValueExtra.Value, out double result))
                            {
                                Value = result;
                            }
                            else
                            {
                                throw new ExpressionException($"Unable to parse number '{token.FinalValueExtra.Value}'");
                            }
                        }
                        break;
                    default:
                        throw new ExpressionException($"Unknown value type {token.FinalValueExtra.Type}");
                }
                return;
            case ExpressionToken.TYPE_FINAL_VARIABLE:
                Path = token.FinalVariableExtra.Path;
                Value = 0;
                return;
            default:
                throw new ExpressionException($"Unexpected parameter type {token.Type} for comparison");
        }
    }
}
