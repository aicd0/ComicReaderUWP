// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Core.Database.SqlHelpers;

public class OrCondition : ICondition
{
    private readonly List<ICondition> _conditions;

    public OrCondition(IEnumerable<ICondition> conditions)
    {
        _conditions = [.. conditions];
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        if (_conditions.Count == 0)
        {
            return "FALSE";
        }

        StringBuilder sb = new();
        for (int i = 0; i < _conditions.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" OR ");
            }
            sb.Append('(').Append(_conditions[i].GetExpression(command)).Append(')');
        }
        return sb.ToString();
    }
}
