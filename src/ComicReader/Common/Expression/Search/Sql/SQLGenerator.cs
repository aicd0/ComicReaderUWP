// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Common.Expression.Search.Sql;

internal static class SQLGenerator
{
    public static ICondition CreateQuery(List<ExpressionToken> tokens, ISQLCommandProvider commandProvider, out List<string> unconsumedKeywords)
    {
        InternalContext context = new();

        List<ICondition> conditions = [];
        foreach (ExpressionToken token in tokens)
        {
            ICondition? condition = CreateQueryCondition(context, token, commandProvider);
            if (condition is not null)
            {
                conditions.Add(condition);
            }
        }

        ICondition? additionalCondition = commandProvider.GetAdditionalCondition();
        if (additionalCondition is not null)
        {
            conditions.Add(additionalCondition);
        }

        ICondition finalCondition = new AndCondition(conditions);
        unconsumedKeywords = context.UnconsumedKeywords;
        return finalCondition;
    }

    private static ICondition? CreateQueryCondition(InternalContext context, ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        if (token.Level != ExpressionToken.LEVEL_FINAL)
        {
            throw new ExpressionException("The token must be at the final level.");
        }

        return token.Type switch
        {
            ExpressionToken.TYPE_FINAL_EMPTY => new BooleanCondition(true),
            ExpressionToken.TYPE_FINAL_VALUE => CreateValueCondition(context, token),
            ExpressionToken.TYPE_FINAL_FILTER => CreateFilterCondition(token, commandProvider),
            _ => throw new ExpressionException($"Unknown token type: {token.Type}"),
        };
    }

    private static ICondition? CreateValueCondition(InternalContext context, ExpressionToken token)
    {
        context.UnconsumedKeywords.Add(token.FinalValueExtra.Value);
        return null;
    }

    private static ICondition CreateFilterCondition(ExpressionToken token, ISQLCommandProvider commandProvider)
    {
        ICondition condition = commandProvider.CreateFilterCondition(token.FinalFilterExtra.Key, token.FinalFilterExtra.Value);
        if (token.FinalFilterExtra.Inverse)
        {
            condition = new NotCondition(condition);
        }

        return condition;
    }

    private class InternalContext
    {
        public readonly List<string> UnconsumedKeywords = [];
    }
}
