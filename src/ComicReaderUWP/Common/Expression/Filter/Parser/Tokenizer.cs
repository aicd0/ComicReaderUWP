// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReaderUWP.Common.Expression.Filter.Parser;

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
                index++;
                continue;
            }
            if (IsNamingCharacter(currentChar))
            {
                ParseNaming(expression, tokens, ref index);
                continue;
            }
            if (IsDigit(currentChar))
            {
                ParseDigit(expression, tokens, ref index);
                continue;
            }
            switch (currentChar)
            {
                case '(':
                    tokens.AddLast(ExpressionToken.CreateRawLeftParenthesis());
                    index++;
                    continue;
                case ')':
                    tokens.AddLast(ExpressionToken.CreateRawRightParenthesis());
                    index++;
                    continue;
                case '"':
                    tokens.AddLast(ParseQuatedString(expression, ref index));
                    continue;
                case '%':
                    tokens.AddLast(ParseVariable(expression, ref index));
                    continue;
                case ',':
                case '=':
                    tokens.AddLast(ExpressionToken.CreateRawOperator(currentChar.ToString()));
                    index++;
                    continue;
                case '>':
                case '<':
                    ParseAngledQuote(expression, tokens, ref index);
                    continue;
                default:
                    break;
            }
            throw new ExpressionException($"Unexpected character '{currentChar}' at position {index}");
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
                    if (IsNamingCharacter(currentChar) || IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '(')
                    {
                        string name = nameBuilder.ToString();
                        if (ParserUtils.IsKeyword(name))
                        {
                            tokens.AddLast(ExpressionToken.CreateRawName(name));
                        }
                        else
                        {
                            tokens.AddLast(ExpressionToken.CreateRawFunc(name));
                        }
                        return;
                    }
                    else
                    {
                        string name = nameBuilder.ToString();
                        tokens.AddLast(ExpressionToken.CreateRawName(name));
                        return;
                    }
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing '{nameBuilder}'");
            }
            nameBuilder.Append(currentChar);
            index++;
        }
    }

    private static void ParseDigit(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder numberBuilder = new();
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException($"Unexpected end of expression while parsing '{numberBuilder}'");
                }
                if (state == 1)
                {
                    tokens.AddLast(ExpressionToken.CreateFinalNumberLiteralInteger(numberBuilder.ToString()));
                }
                else
                {
                    tokens.AddLast(ExpressionToken.CreateFinalNumberLiteralDecimal(numberBuilder.ToString()));
                }
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '.')
                    {
                        state = 2;
                        break;
                    }
                    else if (!IsNamingCharacter(currentChar))
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalNumberLiteralInteger(numberBuilder.ToString()));
                        return;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '{numberBuilder}'");
                    }
                case 2:
                    if (IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '.')
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '{numberBuilder}'");
                    }
                    else if (!IsNamingCharacter(currentChar))
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalNumberLiteralDecimal(numberBuilder.ToString()));
                        return;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '{numberBuilder}'");
                    }
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing '{numberBuilder}'");
            }
            numberBuilder.Append(currentChar);
            index++;
        }
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
                throw new ExpressionException($"Unexpected end of expression while parsing quoted string '{stringBuilder}'");
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

    private static void ParseAngledQuote(string expression, LinkedList<ExpressionToken> tokens, ref int index)
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
                tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (currentChar == '=')
                    {
                        state = 2;
                    }
                    else
                    {
                        tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                        return;
                    }
                    break;
                case 2:
                    tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                    return;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing '{nameBuilder}'");
            }
            nameBuilder.Append(currentChar);
            index++;
        }
    }

    private static ExpressionToken ParseVariable(string expression, ref int index)
    {
        int state = 0;
        List<string> pathList = [];
        StringBuilder nameBuilder = new();
        index++;
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException($"Unexpected end of expression while parsing variable '{nameBuilder}'");
                }
                if (state == 1)
                {
                    pathList.Add(nameBuilder.ToString());
                }
                return ExpressionToken.CreateFinalVariable(pathList);
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    if (IsNamingCharacter(currentChar))
                    {
                        nameBuilder.Append(currentChar);
                        state = 1;
                    }
                    else if (currentChar == '"')
                    {
                        ExpressionToken stringToken = ParseQuatedString(expression, ref index);
                        pathList.Add(stringToken.FinalValueExtra.Value);
                        state = 2;
                        continue;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '{nameBuilder}'");
                    }
                    break;
                case 1:
                    if (IsNamingCharacter(currentChar) || IsDigit(currentChar))
                    {
                        nameBuilder.Append(currentChar);
                    }
                    else if (currentChar == '.')
                    {
                        state = 0;
                        pathList.Add(nameBuilder.ToString());
                        nameBuilder.Clear();
                    }
                    else
                    {
                        pathList.Add(nameBuilder.ToString());
                        return ExpressionToken.CreateFinalVariable(pathList);
                    }
                    break;
                case 2:
                    if (currentChar == '.')
                    {
                        state = 0;
                    }
                    else if (IsNamingCharacter(currentChar) || IsDigit(currentChar))
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '{nameBuilder}.'");
                    }
                    else
                    {
                        return ExpressionToken.CreateFinalVariable(pathList);
                    }
                    break;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing variable '{nameBuilder}'");
            }
            index++;
        }
    }

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }

    private static bool IsNamingCharacter(char c)
    {
        return !char.IsWhiteSpace(c) && (char.IsLetter(c) || c == '_' || !char.IsAscii(c));
    }

    private static bool IsDigit(char c)
    {
        return char.IsDigit(c);
    }
}
