// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Database.SqlHelpers;

public class LikeCondition : ICondition
{
    private readonly string _columnName;
    private readonly string _pattern;

    public LikeCondition(IColumn<string> column, string pattern)
    {
        _columnName = column.Name;
        _pattern = pattern;
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        string parameterName = command.AppendParameter(_pattern);
        return $"{_columnName} LIKE {parameterName}";
    }
}
