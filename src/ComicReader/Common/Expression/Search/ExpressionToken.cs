// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.Common.Expression.Search.Tokens;

namespace ComicReader.Common.Expression.Search;

internal sealed class ExpressionToken
{
    public const int LEVEL_RAW = 0;
    public const int TYPE_RAW_NAME = 0;
    public const int TYPE_RAW_OPERATOR = 1;

    public const int LEVEL_INTERMEDIATE = 1;
    public const int TYPE_INTERMEDIATE_FILTER = 0;

    public const int LEVEL_FINAL = 2;
    public const int TYPE_FINAL_EMPTY = 0;
    public const int TYPE_FINAL_VALUE = 1;
    public const int TYPE_FINAL_FILTER = 2;

    private readonly ITokenExtra? _extra;

    public int Level;
    public int Type;

    public ITokenExtra? Extra => _extra;
    public RawNameTokenExtra RawNameExtra => (RawNameTokenExtra)_extra!;
    public RawOperatorTokenExtra RawOperatorExtra => (RawOperatorTokenExtra)_extra!;
    public IntermediateFilterTokenExtra IntermediateFilterExtra => (IntermediateFilterTokenExtra)_extra!;
    public FinalValueTokenExtra FinalValueExtra => (FinalValueTokenExtra)_extra!;
    public FinalFilterTokenExtra FinalFilterExtra => (FinalFilterTokenExtra)_extra!;

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

    public static ExpressionToken CreateRawOperator(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_OPERATOR, new RawOperatorTokenExtra(name));
    }

    public static ExpressionToken CreateIntermediateFilter(string key, string value)
    {
        return new ExpressionToken(LEVEL_INTERMEDIATE, TYPE_INTERMEDIATE_FILTER, new IntermediateFilterTokenExtra(key, value));
    }

    public static ExpressionToken CreateFinalEmpty()
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_EMPTY, null);
    }

    public static ExpressionToken CreateFinalStringLiteral(string value)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(value));
    }

    public static ExpressionToken CreateFinalFilter(string key, string value, bool inverse)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_FILTER, new FinalFilterTokenExtra(key, value, inverse));
    }
}
