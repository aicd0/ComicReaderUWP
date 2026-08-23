// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReaderUWP.Common.Expression;

internal class ExpressionParser
{
    public static Filter.ExpressionToken ParseFilter(string expression, bool includeHidden = true)
    {
        LinkedList<Filter.ExpressionToken> tokens = [];
        Filter.Parser.Tokenizer.Tokenize(expression, tokens);
        Filter.ExpressionToken token = Filter.Parser.Evaluator.Evaluate(tokens);

        if (!includeHidden)
        {
            token = token.Type == Filter.ExpressionToken.TYPE_FINAL_EMPTY
                ? CreateHiddenFilterToken()
                : Filter.ExpressionToken.CreateFinalFunction(
                    Filter.Parser.ParserUtils.KEYWORD_AND,
                    [token, CreateHiddenFilterToken()]);
        }

        token = Filter.Parser.Optimizer.Optimize(token);
        return token;
    }

    public static List<Search.ExpressionToken> ParseSearch(string expression)
    {
        LinkedList<Search.ExpressionToken> tokens = [];
        Search.Parser.Tokenizer.Tokenize(expression, tokens);
        Search.Parser.Evaluator.Evaluate(tokens);
        return [.. tokens];
    }

    private static Filter.ExpressionToken CreateHiddenFilterToken()
    {
        return Filter.ExpressionToken.CreateFinalFunction(
            Filter.Parser.ParserUtils.OPERATOR_EQUAL,
            [
                Filter.ExpressionToken.CreateFinalVariable(["hidden"]),
                Filter.ExpressionToken.CreateFinalValue(Filter.Tokens.FinalValueTokenExtra.TypeEnum.False),
            ]);
    }
}
