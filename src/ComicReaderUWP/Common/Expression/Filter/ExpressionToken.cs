// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using ComicReaderUWP.Common.Expression.Filter.Tokens;

namespace ComicReaderUWP.Common.Expression.Filter;

internal sealed class ExpressionToken
{
    public const int LEVEL_RAW = 0;
    public const int TYPE_RAW_NAME = 0;
    public const int TYPE_RAW_LEFT_PARENTHESIS = 1;
    public const int TYPE_RAW_RIGHT_PARENTHESIS = 2;
    public const int TYPE_RAW_OPERATOR = 3;
    public const int TYPE_RAW_FUNC = 4;

    public const int LEVEL_INTERMEDIATE = 1;
    public const int TYPE_INTERMEDIATE_EXPRESSION_LIST = 0;

    public const int LEVEL_FINAL = 2;
    public const int TYPE_FINAL_EMPTY = 0;
    public const int TYPE_FINAL_FUNCTION = 1;
    public const int TYPE_FINAL_VALUE = 2;
    public const int TYPE_FINAL_VARIABLE = 3;
    public const int TYPE_FINAL_LIST = 4;

    private readonly ITokenExtra? _extra;

    public int Level;
    public int Type;

    public ITokenExtra? Extra => _extra;
    public RawNameTokenExtra RawNameExtra => (RawNameTokenExtra)_extra!;
    public RawOperatorTokenExtra RawOperatorExtra => (RawOperatorTokenExtra)_extra!;
    public RawFuncTokenExtra RawFuncExtra => (RawFuncTokenExtra)_extra!;
    public IntermediateExpressionListTokenExtra IntermediateExpressionListExtra => (IntermediateExpressionListTokenExtra)_extra!;
    public FinalFuncTokenExtra FinalFuncTokenExtra => (FinalFuncTokenExtra)_extra!;
    public FinalValueTokenExtra FinalValueExtra => (FinalValueTokenExtra)_extra!;
    public FinalVariableTokenExtra FinalVariableExtra => (FinalVariableTokenExtra)_extra!;
    public FinalListTokenExtra FinalListTokenExtra => (FinalListTokenExtra)_extra!;

    private ExpressionToken(int level, int type, ITokenExtra? extra)
    {
        Level = level;
        Type = type;
        _extra = extra;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        _extra?.ToString(sb);
        return sb.ToString();
    }

    public static ExpressionToken CreateRawName(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_NAME, new RawNameTokenExtra(name));
    }

    public static ExpressionToken CreateRawLeftParenthesis()
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_LEFT_PARENTHESIS, null);
    }

    public static ExpressionToken CreateRawRightParenthesis()
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_RIGHT_PARENTHESIS, null);
    }

    public static ExpressionToken CreateRawOperator(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_OPERATOR, new RawOperatorTokenExtra(name));
    }

    public static ExpressionToken CreateRawFunc(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_FUNC, new RawFuncTokenExtra(name));
    }

    public static ExpressionToken CreateIntermediateExpressionList(IEnumerable<ExpressionToken> expressions)
    {
        return new ExpressionToken(LEVEL_INTERMEDIATE, TYPE_INTERMEDIATE_EXPRESSION_LIST, new IntermediateExpressionListTokenExtra(expressions));
    }

    public static ExpressionToken CreateFinalEmpty()
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_EMPTY, null);
    }

    public static ExpressionToken CreateFinalFunction(string name, IEnumerable<ExpressionToken> parameters)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_FUNCTION, new FinalFuncTokenExtra(name, parameters));
    }

    public static ExpressionToken CreateFinalValue(FinalValueTokenExtra.TypeEnum type)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(type, ""));
    }

    public static ExpressionToken CreateFinalNumberLiteralInteger(string number)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.NumberLiteralInteger, number));
    }

    public static ExpressionToken CreateFinalNumberLiteralDecimal(string number)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.NumberLiteralDecimal, number));
    }

    public static ExpressionToken CreateFinalStringLiteral(string value)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.StringLiteral, value));
    }

    public static ExpressionToken CreateFinalVariable(List<string> path)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VARIABLE, new FinalVariableTokenExtra(path));
    }

    public static ExpressionToken CreateFinalList(IEnumerable<ExpressionToken> expressions)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_LIST, new FinalListTokenExtra(expressions));
    }
}
