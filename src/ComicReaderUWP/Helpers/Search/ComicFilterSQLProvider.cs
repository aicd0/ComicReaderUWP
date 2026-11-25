// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Expression.Filter.Sql;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Helpers.Search;

internal class ComicFilterSQLProvider : ISQLCommandProvider
{
    #region Public Methods

    public ICondition CreateComparisonCondition(VariableOrValue left, VariableOrValue right, ComparisonTypeEnum comparisonType)
    {
        if (left.Path is null && right.Path is not null && IsReleaseDatePath(right.Path))
        {
            return CreateReleaseDateComparison(left.Value, comparisonType, valueOnLeft: true);
        }

        if (right.Path is null && left.Path is not null && IsReleaseDatePath(left.Path))
        {
            return CreateReleaseDateComparison(right.Value, comparisonType, valueOnLeft: false);
        }

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
                    if (IsReleaseDatePath(leftItem.Path))
                    {
                        conditions.Add(CreateReleaseDateInCondition(rightValueLiterals));
                    }
                    else
                    {
                        conditions.Add(CreateCondition(leftItem.Path, (column) => new InCondition(ColumnOrValue.FromColumn(column), rightValueLiterals)));
                    }
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

    #endregion

    #region Date time

    private static bool IsReleaseDatePath(IReadOnlyList<string> path)
    {
        return path.Count == 1 && path[0].ToLower() == ComicSQLProviderUtils.VAR_RELEASE_DATE;
    }

    private static ICondition CreateReleaseDateComparison(object value, ComparisonTypeEnum comparisonType, bool valueOnLeft)
    {
        if (!TryParseReleaseDateRange(value, out DateTime start, out DateTime endExclusive))
        {
            return valueOnLeft ?
                new ComparisonCondition(ColumnOrValue.FromValue(value), ColumnOrValue.FromColumn(ComicTable.ColumnReleaseDate), ToSQLComparisonType(comparisonType)) :
                new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnReleaseDate), ColumnOrValue.FromValue(value), ToSQLComparisonType(comparisonType));
        }

        return CreateReleaseDateRangeCondition(start, endExclusive, valueOnLeft ? FlipComparisonType(comparisonType) : comparisonType);
    }

    private static ICondition CreateReleaseDateInCondition(List<object?> values)
    {
        List<ICondition> conditions = [];
        foreach (object? value in values)
        {
            if (value is not null)
            {
                conditions.Add(CreateReleaseDateComparison(value, ComparisonTypeEnum.Equal, valueOnLeft: false));
            }
        }

        return conditions.Count == 0 ? new BooleanCondition(false) : new OrCondition(conditions);
    }

    private static ICondition CreateReleaseDateRangeCondition(DateTime start, DateTime endExclusive, ComparisonTypeEnum comparisonType)
    {
        return comparisonType switch
        {
            ComparisonTypeEnum.Equal => new AndCondition((ICondition[])[
                CreateReleaseDateBoundCondition(start, ComparisonCondition.TypeEnum.GreaterThanOrEqual),
                CreateReleaseDateBoundCondition(endExclusive, ComparisonCondition.TypeEnum.LessThan),
            ]),
            ComparisonTypeEnum.GreaterThan => CreateReleaseDateBoundCondition(endExclusive, ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComparisonTypeEnum.GreaterThanOrEqual => CreateReleaseDateBoundCondition(start, ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComparisonTypeEnum.LessThan => CreateReleaseDateBoundCondition(start, ComparisonCondition.TypeEnum.LessThan),
            ComparisonTypeEnum.LessThanOrEqual => CreateReleaseDateBoundCondition(endExclusive, ComparisonCondition.TypeEnum.LessThan),
            _ => throw new ExpressionException("Unsupported comparison type."),
        };
    }

    private static ICondition CreateReleaseDateBoundCondition(DateTime bound, ComparisonCondition.TypeEnum type)
    {
        return new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnReleaseDate), ColumnOrValue.FromValue(ComicHandle.FormatReleaseDate(bound)), type);
    }

    private static ComparisonTypeEnum FlipComparisonType(ComparisonTypeEnum type)
    {
        return type switch
        {
            ComparisonTypeEnum.GreaterThan => ComparisonTypeEnum.LessThan,
            ComparisonTypeEnum.GreaterThanOrEqual => ComparisonTypeEnum.LessThanOrEqual,
            ComparisonTypeEnum.LessThan => ComparisonTypeEnum.GreaterThan,
            ComparisonTypeEnum.LessThanOrEqual => ComparisonTypeEnum.GreaterThanOrEqual,
            _ => type,
        };
    }

    private static bool TryParseReleaseDateRange(object value, out DateTime start, out DateTime endExclusive)
    {
        start = default;
        endExclusive = default;

        string? text = value switch
        {
            string textValue => textValue.Trim(),
            long numberValue => numberValue.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] parts = text.Split('-', '/');
        if (parts.Length > 3)
        {
            return false;
        }

        int year;
        int month = 1;
        int day = 1;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            return false;
        }
        if (parts.Length >= 2 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out month))
        {
            return false;
        }
        if (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out day))
        {
            return false;
        }

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        start = new DateTime(year, month, day);
        endExclusive = parts.Length switch
        {
            1 => start.AddYears(1),
            2 => start.AddMonths(1),
            _ => start.AddDays(1),
        };
        return true;
    }

    #endregion

    #region Helpers

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
            ComicSQLProviderUtils.VAR_TITLE => new OrCondition((ICondition[])[
                NotNullAndEmptyCondition(ComicTable.ColumnTitle1),
                NotNullAndEmptyCondition(ComicTable.ColumnTitle2),
            ]),
            ComicSQLProviderUtils.VAR_RATING => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnRating), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComicSQLProviderUtils.VAR_TITLE1 => NotNullAndEmptyCondition(ComicTable.ColumnTitle1),
            ComicSQLProviderUtils.VAR_TITLE2 => NotNullAndEmptyCondition(ComicTable.ColumnTitle2),
            ComicSQLProviderUtils.VAR_PROGRESS => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnProgress), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThanOrEqual),
            ComicSQLProviderUtils.VAR_PAGE_COUNT => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnPageCount), ColumnOrValue.FromValue(0), ComparisonCondition.TypeEnum.GreaterThan),
            ComicSQLProviderUtils.VAR_RELEASE_DATE => NotNullAndEmptyCondition(ComicTable.ColumnReleaseDate),
            ComicSQLProviderUtils.VAR_HIDDEN => new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(true)),
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
            ComicSQLProviderUtils.VAR_TITLE => new OrCondition((ICondition[])[
                conditionCreator(ComicTable.ColumnTitle1),
                conditionCreator(ComicTable.ColumnTitle2),
            ]),
            ComicSQLProviderUtils.VAR_RATING => conditionCreator(ComicTable.ColumnRating),
            ComicSQLProviderUtils.VAR_COMPLETION_STATUS => conditionCreator(ComicTable.ColumnCompletionStatus),
            ComicSQLProviderUtils.VAR_TITLE1 => conditionCreator(ComicTable.ColumnTitle1),
            ComicSQLProviderUtils.VAR_TITLE2 => conditionCreator(ComicTable.ColumnTitle2),
            ComicSQLProviderUtils.VAR_PROGRESS => conditionCreator(ComicTable.ColumnProgress),
            ComicSQLProviderUtils.VAR_PAGE_COUNT => conditionCreator(ComicTable.ColumnPageCount),
            ComicSQLProviderUtils.VAR_RELEASE_DATE => conditionCreator(ComicTable.ColumnReleaseDate),
            ComicSQLProviderUtils.VAR_HIDDEN => conditionCreator(ComicTable.ColumnHidden),
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
        return new AndCondition((ICondition[])[
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

    #endregion
}
