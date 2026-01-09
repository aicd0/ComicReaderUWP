// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Expression.Filter.Parser;
using ComicReaderUWP.Common.Expression.Filter.Tokens;
using ComicReaderUWP.SDK.Database.SqlHelpers;

namespace ComicReaderUWP.Common.Expression.Filter.Sql;

internal static class SQLGenerator
{
    public static ICondition CreateQuery(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        return CreateQueryCondition(token, commandProvider);
    }

    private static ICondition CreateQueryCondition(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        if (token.Level != ExpressionToken.LEVEL_FINAL)
        {
            throw new ExpressionException("The token must be at the final level.");
        }

        return token.Type switch
        {
            ExpressionToken.TYPE_FINAL_EMPTY => new BooleanCondition(true),
            ExpressionToken.TYPE_FINAL_VALUE => CreateValueCondition(token),
            ExpressionToken.TYPE_FINAL_VARIABLE => CreateVariableCondition(token, commandProvider),
            ExpressionToken.TYPE_FINAL_FUNCTION => CreateFunctionCondition(token, commandProvider),
            ExpressionToken.TYPE_FINAL_LIST => CreateListCondition(token, commandProvider),
            _ => throw new ExpressionException($"Unknown token type: {token.Type}"),
        };
    }

    private static ICondition CreateValueCondition(ExpressionToken token)
    {
        if (IsBooleanValueToken(token, true))
        {
            return new BooleanCondition(true);
        }
        if (IsBooleanValueToken(token, false))
        {
            return new BooleanCondition(false);
        }
        throw new ExpressionException($"Cannot create a value node from token '{token}'");
    }

    private static ICondition CreateListCondition(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        List<VariableOrValue> items = [];
        foreach (ExpressionToken expression in token.FinalListTokenExtra.Expressions)
        {
            items.Add(new(expression));
        }
        return commandProvider.CreateListCondition(items);
    }

    private static ICondition CreateVariableCondition(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        return commandProvider.CreateVariableCondition(token.FinalVariableExtra.Path);
    }

    private static ICondition CreateFunctionCondition(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        FinalFuncTokenExtra extra = token.FinalFuncTokenExtra;
        return extra.Name switch
        {
            ParserUtils.KEYWORD_NOT => CreateFuncNotCondition(extra.Parameters, commandProvider),
            ParserUtils.KEYWORD_AND => CreateFuncAndCondition(extra.Parameters, commandProvider),
            ParserUtils.KEYWORD_OR => CreateFuncOrCondition(extra.Parameters, commandProvider),
            ParserUtils.OPERATOR_EQUAL => CreateFuncComparisonCondition(extra.Parameters, ComparisonTypeEnum.Equal, commandProvider),
            ParserUtils.OPERATOR_GREATER_THAN => CreateFuncComparisonCondition(extra.Parameters, ComparisonTypeEnum.GreaterThan, commandProvider),
            ParserUtils.OPERATOR_GREATER_THAN_OR_EQUAL => CreateFuncComparisonCondition(extra.Parameters, ComparisonTypeEnum.GreaterThanOrEqual, commandProvider),
            ParserUtils.OPERATOR_LESS_THAN => CreateFuncComparisonCondition(extra.Parameters, ComparisonTypeEnum.LessThan, commandProvider),
            ParserUtils.OPERATOR_LESS_THAN_OR_EQUAL => CreateFuncComparisonCondition(extra.Parameters, ComparisonTypeEnum.LessThanOrEqual, commandProvider),
            ParserUtils.KEYWORD_IN => CreateFuncInCondition(extra.Parameters, commandProvider),
            _ => throw new ExpressionException($"Unknown function '{extra.Name}'."),
        };
    }

    private static ICondition CreateFuncNotCondition(IReadOnlyList<ExpressionToken> parameters, ISQLCommandProvider commandProvider)
    {
        if (parameters.Count != 1)
        {
            throw new ExpressionException($"'NOT' function must have exactly one parameter, but got {parameters.Count}");
        }
        ICondition child = CreateQueryCondition(parameters[0], commandProvider);
        return new NotCondition(child);
    }

    private static ICondition CreateFuncAndCondition(IReadOnlyList<ExpressionToken> parameters, ISQLCommandProvider commandProvider)
    {
        if (parameters.Count == 0)
        {
            return new BooleanCondition(true);
        }
        List<ICondition> children = [];
        foreach (ExpressionToken parameter in parameters)
        {
            children.Add(CreateQueryCondition(parameter, commandProvider));
        }
        return new AndCondition(children);
    }

    private static ICondition CreateFuncOrCondition(IReadOnlyList<ExpressionToken> parameters, ISQLCommandProvider commandProvider)
    {
        if (parameters.Count == 0)
        {
            return new BooleanCondition(false);
        }
        List<ICondition> children = [];
        foreach (ExpressionToken parameter in parameters)
        {
            children.Add(CreateQueryCondition(parameter, commandProvider));
        }
        return new OrCondition(children);
    }

    private static ICondition CreateFuncComparisonCondition(IReadOnlyList<ExpressionToken> parameters, ComparisonTypeEnum comparisonType, ISQLCommandProvider commandProvider)
    {
        if (parameters.Count != 2)
        {
            throw new ExpressionException($"'{comparisonType}' function must have exactly 2 parameters, but got {parameters.Count}");
        }
        return commandProvider.CreateComparisonCondition(new(parameters[0]), new(parameters[1]), comparisonType);
    }

    private static ICondition CreateFuncInCondition(IReadOnlyList<ExpressionToken> parameters, ISQLCommandProvider commandProvider)
    {
        if (parameters.Count != 2)
        {
            throw new ExpressionException("'IN' function must have exactly 2 parameters");
        }
        ExpressionToken leftToken = parameters[0];
        ExpressionToken rightToken = parameters[1];
        List<VariableOrValue> left = [];
        List<VariableOrValue> right = [];
        if (leftToken.Type == ExpressionToken.TYPE_FINAL_LIST)
        {
            foreach (ExpressionToken parameter in leftToken.FinalListTokenExtra.Expressions)
            {
                left.Add(new(parameter));
            }
        }
        else
        {
            left.Add(new(leftToken));
        }
        if (rightToken.Type == ExpressionToken.TYPE_FINAL_LIST)
        {
            foreach (ExpressionToken parameter in rightToken.FinalListTokenExtra.Expressions)
            {
                right.Add(new(parameter));
            }
        }
        else
        {
            right.Add(new(leftToken));
        }
        return commandProvider.CreateInCondition(left, right);
    }

    private static bool IsBooleanValueToken(ExpressionToken token, bool value)
    {
        FinalValueTokenExtra.TypeEnum expectedValue = value ? FinalValueTokenExtra.TypeEnum.True : FinalValueTokenExtra.TypeEnum.False;
        return token.Type == ExpressionToken.TYPE_FINAL_VALUE &&
               token.FinalValueExtra.Type == expectedValue;
    }
}
