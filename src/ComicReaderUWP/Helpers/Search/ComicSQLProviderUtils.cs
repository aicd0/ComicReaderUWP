// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Helpers.Search;

internal class ComicSQLProviderUtils
{
    public const string VAR_EXP = "exp";
    public const string VAR_TAG = "tag";
    public const string VAR_TITLE = "title";
    public const string VAR_RATING = "rating";
    public const string VAR_COMPLETION_STATE = "completion_state";
    public const string VAR_TITLE1 = "title1";
    public const string VAR_TITLE2 = "title2";
    public const string VAR_PROGRESS = "progress";
    public const string VAR_PAGE_COUNT = "page_count";
    public const string VAR_HIDDEN = "hidden";

    public static ICondition CreateTagCondition(ICondition condition)
    {
        var subquery = SelectCommand.Create(TagTable.Instance);
        subquery.AppendCondition(condition);
        subquery.PutQueryInt64(TagTable.ColumnComicId);
        subquery.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery);
    }

    public static ICondition CreateTagCategoryCondition(string category)
    {
        var subquery = SelectCommand.Create(TagCategoryTable.Instance);
        subquery.AppendCondition(TagCategoryTable.ColumnName, category);
        subquery.PutQueryInt64(TagCategoryTable.ColumnComicId);
        subquery.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery);
    }

    public static ICondition CreateTagInTagCategoryCondition(string category, ICondition condition)
    {
        var subquery1 = SelectCommand.Create(TagCategoryTable.Instance);
        subquery1.AppendCondition(TagCategoryTable.ColumnName, category);
        subquery1.PutQueryInt64(TagCategoryTable.ColumnId);
        subquery1.Distinct();
        var subquery2 = SelectCommand.Create(TagTable.Instance);
        subquery2.AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), subquery1));
        subquery2.AppendCondition(condition);
        subquery2.PutQueryInt64(TagTable.ColumnComicId);
        subquery2.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery2);
    }
}
