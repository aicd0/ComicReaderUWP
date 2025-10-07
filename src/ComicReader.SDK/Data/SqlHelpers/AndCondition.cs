// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class AndCondition : ICondition
{
    private readonly List<ICondition> _conditions;

    public AndCondition(IEnumerable<ICondition> conditions)
    {
        _conditions = [.. conditions];
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        if (_conditions.Count == 0)
        {
            return "TRUE";
        }

        if (_conditions.Count == 1)
        {
            return _conditions[0].GetExpression(command);
        }

        StringBuilder sb = new();
        for (int i = 0; i < _conditions.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" AND ");
            }
            sb.Append('(').Append(_conditions[i].GetExpression(command)).Append(')');
        }

        return sb.ToString();
    }
}
