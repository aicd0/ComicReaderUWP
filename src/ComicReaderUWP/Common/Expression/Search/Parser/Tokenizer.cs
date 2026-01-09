// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReaderUWP.Common.Expression.Search.Parser;

class Tokenizer
{
    public static void Tokenize(string expression, LinkedList<ExpressionToken> tokens)
    {
        int index = 0;
        while (index < expression.Length)
        {
            char currentChar = expression[index];
            if (IsWhiteSpace(currentChar))
            {
                tokens.AddLast(ParseWhitespace(expression, ref index));
                continue;
            }

            switch (currentChar)
            {
                case '"':
                    tokens.AddLast(ParseQuatedString(expression, ref index));
                    continue;
                case ':':
                case '-':
                    tokens.AddLast(ExpressionToken.CreateRawOperator(currentChar.ToString()));
                    index++;
                    continue;
                default:
                    break;
            }

            ParseNaming(expression, tokens, ref index);
        }
    }

    private static void ParseNaming(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder nameBuilder = new();
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException($"Unexpected end of expression while parsing '{nameBuilder}'");
                }

                tokens.AddLast(ExpressionToken.CreateRawName(nameBuilder.ToString()));
                break;
            }

            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (IsWhiteSpace(currentChar) || currentChar == ':')
                    {
                        string name = nameBuilder.ToString();
                        tokens.AddLast(ExpressionToken.CreateRawName(name));
                        return;
                    }
                    break;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing '{nameBuilder}'");
            }

            nameBuilder.Append(currentChar);
            index++;
        }
    }

    private static ExpressionToken ParseWhitespace(string expression, ref int index)
    {
        while (index < expression.Length && IsWhiteSpace(expression[index]))
        {
            index++;
        }

        return ExpressionToken.CreateRawWhitespace();
    }

    private static ExpressionToken ParseQuatedString(string expression, ref int index)
    {
        int state = 0;
        StringBuilder stringBuilder = new();
        index++; // Skip the opening quote
        while (true)
        {
            if (index >= expression.Length)
            {
                return ExpressionToken.CreateFinalStringLiteral(stringBuilder.ToString());
            }

            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    if (currentChar == '"')
                    {
                        index++;
                        return ExpressionToken.CreateFinalStringLiteral(stringBuilder.ToString());
                    }
                    else if (currentChar == '\\')
                    {
                        state = 1; // Escape sequence
                    }
                    else
                    {
                        stringBuilder.Append(currentChar);
                    }
                    break;
                case 1:
                    stringBuilder.Append(currentChar);
                    state = 0; // Reset to normal state
                    break;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing quoted string '{stringBuilder}'");
            }

            index++;
        }
    }

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }
}
