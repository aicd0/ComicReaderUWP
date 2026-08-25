// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Expression.Search.Sql;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Helpers.Search;

internal class ComicSearchSQLProvider(Common.Expression.Filter.Sql.ISQLCommandProvider expProvider) : ISQLCommandProvider
{
    private readonly Common.Expression.Filter.Sql.ISQLCommandProvider _expProvider = expProvider;

    public bool IsHiddenFilter(string key)
    {
        return key == ComicSQLProviderUtils.VAR_HIDDEN;
    }

    public ICondition CreateNotHiddenCondition()
    {
        return new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false));
    }

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
                        return new BooleanCondition(false);
                    }

                    ICondition condition;
                    try
                    {
                        condition = Common.Expression.Filter.Sql.SQLGenerator.CreateQuery(token, _expProvider);
                    }
                    catch (ExpressionException)
                    {
                        return new BooleanCondition(false);
                    }

                    return condition;
                }

            case ComicSQLProviderUtils.VAR_TAG:
                return ComicSQLProviderUtils.CreateTagCondition(new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent), ColumnOrValue.FromValue(value)));

            case ComicSQLProviderUtils.VAR_HIDDEN:
                return value switch
                {
                    "0" => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false)),
                    "1" => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(true)),
                    _ => new BooleanCondition(false),
                };

            default:
                return ComicSQLProviderUtils.CreateTagInTagCategoryCondition(key, new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent), ColumnOrValue.FromValue(value)));
        }
    }
}
