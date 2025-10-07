// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Expression.Filter.Tokens;

namespace ComicReader.Common.Expression.Filter.Parser;

internal class Optimizer
{
    public static ExpressionToken Optimize(ExpressionToken token)
    {
        return OptimizeToken(token);
    }

    private static ExpressionToken OptimizeToken(ExpressionToken token)
    {
        if (token.Level != ExpressionToken.LEVEL_FINAL)
        {
            throw new ExpressionException("The token must be at the final level");
        }

        return token.Type switch
        {
            ExpressionToken.TYPE_FINAL_FUNCTION => OptimizeFunction(token),
            _ => token,
        };
    }

    private static ExpressionToken OptimizeFunction(ExpressionToken token)
    {
        FinalFuncTokenExtra extra = token.FinalFuncTokenExtra;
        List<ExpressionToken> parameters = [.. extra.Parameters];
        for (int i = 0; i < parameters.Count; i++)
        {
            parameters[i] = OptimizeToken(parameters[i]);
        }
        switch (extra.Name)
        {
            case ParserUtils.KEYWORD_AND:
                {
                    List<ExpressionToken> expressions = [];
                    foreach (ExpressionToken parameter in parameters)
                    {
                        if (IsBooleanValueToken(parameter, true))
                        {
                            continue;
                        }
                        if (IsBooleanValueToken(parameter, false))
                        {
                            return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.False);
                        }
                        if (IsFuncToken(parameter, ParserUtils.KEYWORD_AND))
                        {
                            expressions.AddRange(parameter.FinalFuncTokenExtra.Parameters);
                        }
                        else
                        {
                            expressions.Add(parameter);
                        }
                    }
                    if (expressions.Count == 0)
                    {
                        return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.True);
                    }
                    if (expressions.Count == 1)
                    {
                        return expressions[0];
                    }
                    return ExpressionToken.CreateFinalFunction(ParserUtils.KEYWORD_AND, expressions);
                }
            case ParserUtils.KEYWORD_OR:
                {
                    List<ExpressionToken> expressions = [];
                    foreach (ExpressionToken parameter in parameters)
                    {
                        if (IsBooleanValueToken(parameter, false))
                        {
                            continue;
                        }
                        if (IsBooleanValueToken(parameter, true))
                        {
                            return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.True);
                        }
                        if (IsFuncToken(parameter, ParserUtils.KEYWORD_OR))
                        {
                            expressions.AddRange(parameter.FinalFuncTokenExtra.Parameters);
                        }
                        else
                        {
                            expressions.Add(parameter);
                        }
                    }
                    if (expressions.Count == 0)
                    {
                        return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.False);
                    }
                    if (expressions.Count == 1)
                    {
                        return expressions[0];
                    }
                    return ExpressionToken.CreateFinalFunction(ParserUtils.KEYWORD_OR, expressions);
                }
            case ParserUtils.KEYWORD_NOT:
                if (parameters.Count != 1)
                {
                    throw new ExpressionException("'NOT' function must have exactly one parameter");
                }
                if (IsBooleanValueToken(parameters[0], true))
                {
                    return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.False);
                }
                if (IsBooleanValueToken(parameters[0], false))
                {
                    return ExpressionToken.CreateFinalValue(FinalValueTokenExtra.TypeEnum.True);
                }
                if (IsFuncToken(parameters[0], ParserUtils.KEYWORD_NOT))
                {
                    return parameters[0].FinalFuncTokenExtra.Parameters[0];
                }
                break;
            default:
                break;
        }
        return ExpressionToken.CreateFinalFunction(extra.Name, parameters);
    }

    private static bool IsBooleanValueToken(ExpressionToken token, bool value)
    {
        FinalValueTokenExtra.TypeEnum expectedValue = value ? FinalValueTokenExtra.TypeEnum.True : FinalValueTokenExtra.TypeEnum.False;
        return token.Type == ExpressionToken.TYPE_FINAL_VALUE &&
               token.FinalValueExtra.Type == expectedValue;
    }

    private static bool IsFuncToken(ExpressionToken token, string name)
    {
        return token.Level == ExpressionToken.LEVEL_FINAL &&
               token.Type == ExpressionToken.TYPE_FINAL_FUNCTION &&
               token.FinalFuncTokenExtra.Name == name;
    }
}
