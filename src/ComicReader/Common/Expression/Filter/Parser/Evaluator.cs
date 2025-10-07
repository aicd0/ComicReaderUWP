// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Expression.Filter.Tokens;

namespace ComicReader.Common.Expression.Filter.Parser;

internal class Evaluator
{
    public static ExpressionToken Evaluate(LinkedList<ExpressionToken> tokens)
    {
        ExpressionToken token = EvaluateParenthses(tokens);
        if (token.Level != ExpressionToken.LEVEL_FINAL)
        {
            throw new ExpressionException("Unknown error");
        }
        return token;
    }

    private static ExpressionToken EvaluateParenthses(LinkedList<ExpressionToken> tokens)
    {
        LinkedList<ExpressionToken> currentTokens = [];
        Stack<LinkedList<ExpressionToken>> parenthesesStack = [];
        LinkedListNode<ExpressionToken>? node = tokens.First;
        while (node != null)
        {
            ExpressionToken token = node.Value;
            node = node.Next;
            if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_LEFT_PARENTHESIS)
            {
                parenthesesStack.Push(currentTokens);
                currentTokens = [];
            }
            else if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_RIGHT_PARENTHESIS)
            {
                if (parenthesesStack.Count == 0)
                {
                    throw new ExpressionException("Unmatched right parenthesis ')'");
                }
                ExpressionToken evaluated = EvaluateParenthsesLess(currentTokens);
                currentTokens = parenthesesStack.Pop();
                currentTokens.AddLast(evaluated);
            }
            else
            {
                currentTokens.AddLast(token);
            }
        }
        if (parenthesesStack.Count > 0)
        {
            throw new ExpressionException("Unmatched left parenthesis '('");
        }
        return EvaluateParenthsesLess(currentTokens);
    }

    private static ExpressionToken EvaluateParenthsesLess(LinkedList<ExpressionToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return ExpressionToken.CreateFinalEmpty();
        }

        // Parse unquoted string literal
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME && !ParserUtils.IsKeyword(token.RawNameExtra.Name))
                {
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalStringLiteral(token.RawNameExtra.Name));
                    tokens.Remove(node);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse keywords TRUE FALSE
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME)
                {
                    FinalValueTokenExtra.TypeEnum type;
                    switch (token.RawNameExtra.Name.ToUpper())
                    {
                        case ParserUtils.KEYWORD_FALSE:
                            type = FinalValueTokenExtra.TypeEnum.False;
                            break;
                        case ParserUtils.KEYWORD_TRUE:
                            type = FinalValueTokenExtra.TypeEnum.True;
                            break;
                        default:
                            continue;
                    }
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalValue(type));
                    tokens.Remove(node);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse functions
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_FUNC)
                {
                    if (nextNode == null)
                    {
                        throw new ExpressionException("Function call must be followed by a left parenthesis '('");
                    }
                    string functionName = token.RawFuncExtra.Name.ToUpper();
                    ExpressionToken nextToken = nextNode.Value;
                    if (nextToken.Level == ExpressionToken.LEVEL_INTERMEDIATE)
                    {
                        if (nextToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunction(functionName, nextToken.IntermediateExpressionListExtra.Expressions));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                        else
                        {
                            throw new ExpressionException($"Unexpected token after function name: {nextToken}");
                        }
                    }
                    else if (nextToken.Level == ExpressionToken.LEVEL_FINAL)
                    {
                        if (nextToken.Type == ExpressionToken.TYPE_FINAL_EMPTY)
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunction(functionName, []));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                        else
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunction(functionName, [nextToken]));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after function name: {nextToken}");
                    }
                }
            }
        }

        // Parse list
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_INTERMEDIATE && token.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                {
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalList(token.IntermediateExpressionListExtra.Expressions));
                    tokens.Remove(node);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse operators = > < >= <=
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && IsComparisonOperator(token.RawOperatorExtra.Name))
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by tokens on both sides");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsScalarReturnValueToken(previousToken) || !IsScalarReturnValueToken(nextToken))
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by valid tokens on both sides");
                    }
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunction(token.RawOperatorExtra.Name, [previousToken, nextToken]));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse keyword IN
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME)
                {
                    string functionName = token.RawNameExtra.Name.ToUpper();
                    switch (functionName)
                    {
                        case ParserUtils.KEYWORD_IN:
                            break;
                        default:
                            continue;
                    }
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Keyword '{functionName}' must be surrounded by tokens on both sides");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    List<ExpressionToken> parameters = [];

                    if (IsReturnValueToken(previousToken))
                    {
                        parameters.Add(previousToken);
                    }
                    else
                    {
                        throw new ExpressionException($"Keyword '{functionName}' must be preceded by a valid token");
                    }

                    if (nextToken.Level == ExpressionToken.LEVEL_FINAL && nextToken.Type == ExpressionToken.TYPE_FINAL_LIST)
                    {
                        parameters.Add(nextToken);
                    }
                    else if (IsScalarReturnValueToken(nextToken))
                    {
                        parameters.Add(ExpressionToken.CreateFinalList([nextToken]));
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after keyword '{functionName}': {nextToken}");
                    }

                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunction(functionName, parameters));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse keyword NOT
        {
            LinkedListNode<ExpressionToken>? previousNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.Last; node != null; node = previousNode)
            {
                previousNode = node.Previous;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME)
                {
                    string functionName = token.RawNameExtra.Name.ToUpper();
                    switch (functionName)
                    {
                        case ParserUtils.KEYWORD_NOT:
                            break;
                        default:
                            continue;
                    }
                    LinkedListNode<ExpressionToken>? nextNode = node.Next ?? throw new ExpressionException($"Keyword '{functionName}' must be followed by a valid token");
                    ExpressionToken nextToken = nextNode.Value;
                    if (IsScalarReturnValueToken(nextToken))
                    {
                        LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunction(functionName, [nextToken]));
                        tokens.Remove(node);
                        tokens.Remove(nextNode);
                        node = newNode;
                        previousNode = node.Previous;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after keyword '{functionName}': {nextToken}");
                    }
                }
            }
        }

        // Parse keywords AND OR
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME)
                {
                    string functionName = token.RawNameExtra.Name.ToUpper();
                    switch (functionName)
                    {
                        case ParserUtils.KEYWORD_AND:
                        case ParserUtils.KEYWORD_OR:
                            break;
                        default:
                            continue;
                    }
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Keyword '{functionName}' must be surrounded by tokens on both sides");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsScalarReturnValueToken(previousToken) || !IsScalarReturnValueToken(nextToken))
                    {
                        throw new ExpressionException($"Keyword '{functionName}' must be surrounded by final tokens on both sides");
                    }
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunction(functionName, [previousToken, nextToken]));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        // Parse comma
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && token.RawOperatorExtra.Name == ",")
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by tokens on both sides");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    List<ExpressionToken> expressions = [];

                    if (IsScalarReturnValueToken(previousToken))
                    {
                        expressions.Add(previousToken);
                    }
                    else if (previousToken.Level == ExpressionToken.LEVEL_INTERMEDIATE && previousToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                    {
                        expressions.AddRange(previousToken.IntermediateExpressionListExtra.Expressions);
                    }
                    else
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be preceded by a valid token");
                    }

                    if (IsScalarReturnValueToken(nextToken))
                    {
                        expressions.Add(nextToken);
                    }
                    else if (nextToken.Level == ExpressionToken.LEVEL_INTERMEDIATE && nextToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                    {
                        expressions.AddRange(nextToken.IntermediateExpressionListExtra.Expressions);
                    }
                    else
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be followed by a valid token");
                    }

                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateIntermediateExpressionList(expressions));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
            }
        }

        LinkedListNode<ExpressionToken>? firstNode = tokens.First;
        if (firstNode is null)
        {
            return ExpressionToken.CreateFinalEmpty();
        }

        if (tokens.Count != 1)
        {
            throw new ExpressionException("Expression cannot be reduced to a single token");
        }

        return firstNode.Value;
    }

    private static bool IsComparisonOperator(string name)
    {
        return name == ParserUtils.OPERATOR_EQUAL ||
               name == ParserUtils.OPERATOR_LESS_THAN ||
               name == ParserUtils.OPERATOR_LESS_THAN_OR_EQUAL ||
               name == ParserUtils.OPERATOR_GREATER_THAN ||
               name == ParserUtils.OPERATOR_GREATER_THAN_OR_EQUAL;
    }

    private static bool IsReturnValueToken(ExpressionToken token)
    {
        return token.Level == ExpressionToken.LEVEL_FINAL && token.Type != ExpressionToken.TYPE_FINAL_EMPTY;
    }

    private static bool IsScalarReturnValueToken(ExpressionToken token)
    {
        return IsReturnValueToken(token) && token.Type != ExpressionToken.TYPE_FINAL_LIST;
    }
}
