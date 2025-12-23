// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

using ComicReader.Common.Expression;
using ComicReader.Common.Expression.Filter.Sql;
using ComicReader.Data.Tables;
using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Helpers.Search;

internal class ComicFilterSQLProvider : ISQLCommandProvider
{
    public ICondition CreateComparisonCondition(VariableOrValue left, VariableOrValue right, ComparisonTypeEnum comparisonType)
    {
        if (left.Path is null)
        {
            if (right.Path is null)
            {
                return new ComparisonCondition(ColumnOrValue.FromValue(left.Value), ColumnOrValue.FromValue(right.Value), ToSQLComparisonType(comparisonType));
            }
            else
            {
                return CreateCondition(right.Path, (column) => new ComparisonCondition(ColumnOrValue.FromValue(left.Value), ColumnOrValue.FromColumn(column), ToSQLComparisonType(comparisonType)));
            }
        }
        else
        {
            if (right.Path is null)
            {
                return CreateCondition(left.Path, (column) => new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(right.Value), ToSQLComparisonType(comparisonType)));
            }
            else
            {
                return CreateCondition(left.Path, right.Path);
            }
        }
    }

    public ICondition CreateInCondition(List<VariableOrValue> left, List<VariableOrValue> right)
    {
        List<object?> rightValueLiterals = [];
        List<IReadOnlyList<string>> rightPaths = [];
        foreach (VariableOrValue rightItem in right)
        {
            if (rightItem.Path is null)
            {
                rightValueLiterals.Add(rightItem.Value);
            }
            else
            {
                rightPaths.Add(rightItem.Path);
            }
        }

        List<ICondition> conditions = [];
        foreach (VariableOrValue leftItem in left)
        {
            if (leftItem.Path is null)
            {
                if (rightValueLiterals.Count > 0)
                {
                    conditions.Add(new InCondition(ColumnOrValue.FromValue(leftItem.Value), rightValueLiterals));
                }

                foreach (IReadOnlyList<string> rightPath in rightPaths)
                {
                    conditions.Add(CreateCondition(rightPath, (column) => new ComparisonCondition(ColumnOrValue.FromValue(leftItem.Value), ColumnOrValue.FromColumn(column), ToSQLComparisonType(ComparisonTypeEnum.Equal))));
                }
            }
            else
            {
                if (rightValueLiterals.Count > 0)
                {
                    conditions.Add(CreateCondition(leftItem.Path, (column) => new InCondition(ColumnOrValue.FromColumn(column), rightValueLiterals)));
                }

                foreach (IReadOnlyList<string> rightPath in rightPaths)
                {
                    conditions.Add(CreateCondition(leftItem.Path, rightPath));
                }
            }
        }

        return new OrCondition(conditions);
    }

    public ICondition CreateListCondition(List<VariableOrValue> path)
    {
        throw new ExpressionException("List cannot be used as expression");
    }

    public ICondition CreateVariableCondition(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            throw new ExpressionException("Variable path is empty");
        }

        if (path.Count == 1)
        {
            return CreateHasCondition(path[0]);
        }

        if (path.Count == 2)
        {
            return CreateHasCondition(path[0], path[1]);
        }

        if (path.Count == 3)
        {
            return CreateHasCondition(path[0], path[1], path[2]);
        }

        throw new ExpressionException($"Invalid variable path '{ToVariableName(path)}'");
    }

    private static ICondition CreateCondition(IReadOnlyList<string> left, Func<IColumnTypeless, ICondition> conditionCreator)
    {
        if (left.Count == 0)
        {
            throw new ExpressionException("Variable path is empty");
        }

        if (left.Count == 1)
        {
            return CreateCondition(left[0], conditionCreator);
        }

        if (left.Count == 2)
        {
            return CreateCondition(left[0], left[1], conditionCreator);
        }

        throw new ExpressionException($"Invalid variable path '{ToVariableName(left)}'");
    }

    private static ICondition CreateCondition(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        throw new ExpressionException("Variables cannot be on both sides of an operator");
    }

    private static ICondition CreateHasCondition(string path1)
    {
        return path1.ToLower() switch
        {
            ComicSQLProviderUtils.VAR_TAG => ComicSQLProviderUtils.CreateTagCondition(new BooleanCondition(true)),
            ComicSQLProviderUtils.VAR_TITLE => new OrCondition([
                NotNullAndEmptyCondition(ComicTable.ColumnTitle1),
                NotNullAndEmptyCondition(ComicTable.ColumnTitle2),
            ]),
            ComicSQLProviderUtils.VAR_RATING => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnRating), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComicSQLProviderUtils.VAR_TITLE1 => NotNullAndEmptyCondition(ComicTable.ColumnTitle1),
            ComicSQLProviderUtils.VAR_TITLE2 => NotNullAndEmptyCondition(ComicTable.ColumnTitle2),
            ComicSQLProviderUtils.VAR_PROGRESS => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnProgress), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComicSQLProviderUtils.VAR_PAGE_COUNT => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnPageCount), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThan),
            _ => throw new ExpressionException($"Variable '{path1}' cannot be used as a condition"),
        };
    }

    private static ICondition CreateHasCondition(string path1, string path2)
    {
        return path1.ToLower() switch
        {
            ComicSQLProviderUtils.VAR_TAG => ComicSQLProviderUtils.CreateTagCategoryCondition(path2),
            _ => throw new ExpressionException($"Variable '{path1}.{path2}' cannot be used as a condition"),
        };
    }

    private static ICondition CreateHasCondition(string path1, string path2, string path3)
    {
        return path1.ToLower() switch
        {
            ComicSQLProviderUtils.VAR_TAG => ComicSQLProviderUtils.CreateTagInTagCategoryCondition(path2,
                new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent), ColumnOrValue.FromValue(path3))),
            _ => throw new ExpressionException($"Variable '{path1}.{path2}.{path3}' cannot be used as a condition"),
        };
    }

    private static ICondition CreateCondition(string path1, Func<IColumnTypeless, ICondition> conditionCreator)
    {
        return path1.ToLower() switch
        {
            ComicSQLProviderUtils.VAR_TAG => ComicSQLProviderUtils.CreateTagCondition(conditionCreator(TagTable.ColumnContent)),
            ComicSQLProviderUtils.VAR_TITLE => new OrCondition([
                conditionCreator(ComicTable.ColumnTitle1),
                conditionCreator(ComicTable.ColumnTitle2),
            ]),
            ComicSQLProviderUtils.VAR_RATING => conditionCreator(ComicTable.ColumnRating),
            ComicSQLProviderUtils.VAR_COMPLETION_STATE => conditionCreator(ComicTable.ColumnCompletionState),
            ComicSQLProviderUtils.VAR_TITLE1 => conditionCreator(ComicTable.ColumnTitle1),
            ComicSQLProviderUtils.VAR_TITLE2 => conditionCreator(ComicTable.ColumnTitle2),
            ComicSQLProviderUtils.VAR_PROGRESS => conditionCreator(ComicTable.ColumnProgress),
            ComicSQLProviderUtils.VAR_PAGE_COUNT => conditionCreator(ComicTable.ColumnPageCount),
            _ => throw new ExpressionException($"Variable '{path1}' cannot be used here"),
        };
    }

    private static ICondition CreateCondition(string path1, string path2, Func<IColumnTypeless, ICondition> conditionCreator)
    {
        return path1.ToLower() switch
        {
            ComicSQLProviderUtils.VAR_TAG => ComicSQLProviderUtils.CreateTagInTagCategoryCondition(path2, conditionCreator(TagTable.ColumnContent)),
            _ => throw new ExpressionException($"Variable '{path1}.{path2}' cannot be used here"),
        };
    }

    private static ICondition NotNullAndEmptyCondition(IColumnTypeless column)
    {
        return new AndCondition([
            new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(null), ComparisonCondition.TypeEnum.IsNot),
            new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(""), ComparisonCondition.TypeEnum.NotEqual),
        ]);
    }

    private static ComparisonCondition.TypeEnum ToSQLComparisonType(ComparisonTypeEnum type)
    {
        return type switch
        {
            ComparisonTypeEnum.Equal => ComparisonCondition.TypeEnum.Equal,
            ComparisonTypeEnum.GreaterThan => ComparisonCondition.TypeEnum.GreaterThan,
            ComparisonTypeEnum.GreaterThanOrEqual => ComparisonCondition.TypeEnum.GreaterThanOrEqual,
            ComparisonTypeEnum.LessThan => ComparisonCondition.TypeEnum.LessThan,
            ComparisonTypeEnum.LessThanOrEqual => ComparisonCondition.TypeEnum.LessThanOrEqual,
            _ => throw new ExpressionException("Unsupported comparison type.")
        };
    }

    private static string ToVariableName(IReadOnlyList<string> path)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('.');
            }
            sb.Append(path[i]);
        }
        return sb.ToString();
    }
}
