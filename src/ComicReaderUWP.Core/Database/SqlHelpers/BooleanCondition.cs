// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public class BooleanCondition : ICondition
{
    private readonly bool _value;

    public BooleanCondition(bool value)
    {
        _value = value;
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        return _value ? "TRUE" : "FALSE";
    }
}
