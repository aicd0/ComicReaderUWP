// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.Expression.Search.Parser;

internal class Evaluator
{
    public static void Evaluate(LinkedList<ExpressionToken> tokens)
    {
        EvaluateInternal(tokens);
    }

    private static void EvaluateInternal(LinkedList<ExpressionToken> tokens)
    {
        if (tokens.Count == 0)
        {
            tokens.AddFirst(ExpressionToken.CreateFinalEmpty());
            return;
        }

        // Parse filter XXX:XXX
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && token.RawOperatorExtra.Name == ":")
                {
                    void ReplaceWithString()
                    {
                        LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalStringLiteral(":"));
                        tokens.Remove(node);
                        nextNode = newNode.Next;
                    }

                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        ReplaceWithString();
                        continue;
                    }

                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsRawNameToken(previousToken) || !(IsRawNameToken(nextToken) || IsFinalValueToken(nextToken)))
                    {
                        ReplaceWithString();
                        continue;
                    }

                    string key = previousToken.RawNameExtra.Name;
                    string value = IsRawNameToken(nextToken) ? nextToken.RawNameExtra.Name : nextToken.FinalValueExtra.Value;
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateIntermediateFilter(key, value));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    nextNode = newNode.Next;
                }
            }
        }

        // Parse inverse operator -
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && token.RawOperatorExtra.Name == "-")
                {
                    void ReplaceWithString()
                    {
                        LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalStringLiteral("-"));
                        tokens.Remove(node);
                        nextNode = newNode.Next;
                    }

                    if (nextNode == null)
                    {
                        ReplaceWithString();
                        continue;
                    }

                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsIntermediateFilterToken(nextToken))
                    {
                        ReplaceWithString();
                        continue;
                    }

                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFilter(
                        nextToken.IntermediateFilterExtra.Key, nextToken.IntermediateFilterExtra.Value, true));
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    nextNode = newNode.Next;
                }
            }
        }

        // Parse intermediate filter
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_INTERMEDIATE && token.Type == ExpressionToken.TYPE_INTERMEDIATE_FILTER)
                {
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFilter(
                        token.IntermediateFilterExtra.Key, token.IntermediateFilterExtra.Value, false));
                    tokens.Remove(node);
                    nextNode = newNode.Next;
                }
            }
        }

        // Remove whitespace
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_WHITESPACE)
                {
                    tokens.Remove(node);
                }
            }
        }

        // Parse raw name
        {
            LinkedListNode<ExpressionToken>? nextNode;
            for (LinkedListNode<ExpressionToken>? node = tokens.First; node != null; node = nextNode)
            {
                nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME)
                {
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalStringLiteral(token.RawNameExtra.Name));
                    tokens.Remove(node);
                    nextNode = newNode.Next;
                }
            }
        }

        foreach (ExpressionToken token in tokens)
        {
            if (token.Level != ExpressionToken.LEVEL_FINAL)
            {
                throw new ExpressionException("Unknown error");
            }
        }
    }

    private static bool IsRawNameToken(ExpressionToken token)
    {
        return token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_NAME;
    }

    private static bool IsFinalValueToken(ExpressionToken token)
    {
        return token.Level == ExpressionToken.LEVEL_FINAL && token.Type == ExpressionToken.TYPE_FINAL_VALUE;
    }

    private static bool IsIntermediateFilterToken(ExpressionToken token)
    {
        return token.Level == ExpressionToken.LEVEL_INTERMEDIATE && token.Type == ExpressionToken.TYPE_INTERMEDIATE_FILTER;
    }
}
