// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Expression;
using ComicReader.Common.Expression.Search.Sql;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Helpers.Search;

internal class ComicSearchSQLProvider(Common.Expression.Filter.Sql.ISQLCommandProvider expProvider) : ISQLCommandProvider
{

    private readonly Common.Expression.Filter.Sql.ISQLCommandProvider _expProvider = expProvider;
    private bool _hasHiddenCondition = false;

    public ICondition CreateFilterCondition(string key, string value)
    {
        switch (key)
        {
            case ComicSQLProviderUtils.VAR_EXP:
                {
                    Common.Expression.Filter.ExpressionToken token;
                    try
                    {
                        token = ExpressionParser.ParseFilter(value);
                    }
                    catch (ExpressionException)
                    {
                        break;
                    }

                    ICondition condition;
                    try
                    {
                        condition = Common.Expression.Filter.Sql.SQLGenerator.CreateQuery(token, _expProvider);
                    }
                    catch (ExpressionException)
                    {
                        break;
                    }

                    return condition;
                }
            case ComicSQLProviderUtils.VAR_TAG:
                return ComicSQLProviderUtils.CreateTagCondition(new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent), ColumnOrValue.FromValue(value)));
            case ComicSQLProviderUtils.VAR_HIDDEN:
                if (value == "1")
                {
                    _hasHiddenCondition = true;
                    return new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(true));
                }
                else if (value == "0")
                {
                    _hasHiddenCondition = true;
                    return new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false));
                }
                break;
            default:
                break;
        }

        return ComicSQLProviderUtils.CreateTagInTagCategoryCondition(key, new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent), ColumnOrValue.FromValue(value)));
    }

    public ICondition? GetAdditionalCondition()
    {
        if (!_hasHiddenCondition)
        {
            return new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false));
        }

        return null;
    }
}
