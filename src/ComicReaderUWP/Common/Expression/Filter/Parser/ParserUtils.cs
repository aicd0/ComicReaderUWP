// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.Expression.Filter.Parser;

internal class ParserUtils
{
    public const string KEYWORD_IN = "IN";
    public const string KEYWORD_NOT = "NOT";
    public const string KEYWORD_AND = "AND";
    public const string KEYWORD_OR = "OR";
    public const string KEYWORD_TRUE = "TRUE";
    public const string KEYWORD_FALSE = "FALSE";

    public const string OPERATOR_EQUAL = "=";
    public const string OPERATOR_LESS_THAN = "<";
    public const string OPERATOR_LESS_THAN_OR_EQUAL = "<=";
    public const string OPERATOR_GREATER_THAN = ">";
    public const string OPERATOR_GREATER_THAN_OR_EQUAL = ">=";

    public static bool IsKeyword(string name)
    {
        return name.ToUpper() switch
        {
            KEYWORD_IN => true,
            KEYWORD_NOT => true,
            KEYWORD_OR => true,
            KEYWORD_AND => true,
            KEYWORD_TRUE => true,
            KEYWORD_FALSE => true,
            _ => false,
        };
    }
}
