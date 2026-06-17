// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Helpers.Search;

internal class ComicSearchEngine
{
    private const string TAG = nameof(ComicSearchEngine);

    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.DefaultQueue;
    private int _updateSubmitted = 0;

    private Action<IReadOnlyList<ComicModel>>? _resultCallback = null;
    private readonly List<ComicModel> _comicItems = [];

    private volatile string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => _searchText = value;
    }

    private volatile string _expression = string.Empty;
    public string Expression
    {
        get => _expression;
        set => _expression = value;
    }

    public void SetResultCallback(Action<IReadOnlyList<ComicModel>>? callback)
    {
        _resultCallback = callback;
    }

    public void Update()
    {
        ScheduleUpdate();
    }

    private void ScheduleUpdate()
    {
        if (Interlocked.CompareExchange(ref _updateSubmitted, 1, 0) == 1)
        {
            return;
        }

        _dispatcher.SubmitAsync(async () =>
        {
            Interlocked.Exchange(ref _updateSubmitted, 0);
            await UpdateNoLock();
        });
    }

    private async Task UpdateNoLock()
    {
        Logger.I(TAG, "UpdateNoLock");

        string searchText = _searchText;
        string expression = _expression;
        ICondition? expressionCondition = ParseExpression(expression);

        List<long> ids = await SearchByKeywords(searchText, expressionCondition);
        List<ComicModel> comicItems = await ComicModel.BatchFromId(ids);

        Dictionary<long, int> order = [];
        for (int i = 0; i < ids.Count; i++)
        {
            order[ids[i]] = i;
        }

        comicItems.Sort(delegate (ComicModel x, ComicModel y)
        {
            if (!order.TryGetValue(x.Id, out int xIndex))
            {
                xIndex = int.MaxValue;
            }

            if (!order.TryGetValue(y.Id, out int yIndex))
            {
                yIndex = int.MaxValue;
            }

            return xIndex - yIndex;
        });

        _comicItems.Clear();
        _comicItems.AddRange(comicItems);
        _resultCallback?.Invoke(comicItems);
    }

    private ICondition? ParseExpression(string expression)
    {
        Common.Expression.Filter.ExpressionToken token;
        try
        {
            token = ExpressionParser.ParseFilter(expression);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        ICondition condition;
        try
        {
            condition = Common.Expression.Filter.Sql.SQLGenerator.CreateQuery(token, new ComicFilterSQLProvider());
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        return condition;
    }

    private ICondition? ParseSearchExpresssion(string expression, out List<string> remainingKeywords)
    {
        List<Common.Expression.Search.ExpressionToken> tokens;
        try
        {
            tokens = ExpressionParser.ParseSearch(expression);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            remainingKeywords = [.. expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            return null;
        }

        ICondition condition;
        try
        {
            condition = Common.Expression.Search.Sql.SQLGenerator.CreateQuery(tokens, new ComicSearchSQLProvider(new ComicFilterSQLProvider()), out remainingKeywords);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            remainingKeywords = [.. expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            return null;
        }

        return condition;
    }

    private async Task<List<long>> SearchByKeywords(string searchText, ICondition? additionalCondition)
    {
        ICondition? searchCondition = ParseSearchExpresssion(searchText, out List<string> remaining);
        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i] = remaining[i].ToLowerInvariant();
        }

        var matches = new List<Match>();
        await ComicHandle.Enqueue(() =>
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            IReaderToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            IReaderToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);

            if (searchCondition is not null)
            {
                command.AppendCondition(searchCondition);
            }

            if (additionalCondition is not null)
            {
                command.AppendCondition(additionalCondition);
            }

            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                // Calculate similarity.
                int similarity = 0;
                string title1 = title1Token.GetValue();
                string title2 = title2Token.GetValue();

                if (remaining.Count > 0)
                {
                    string matchText = (title1 + " " + title2).ToLowerInvariant();
                    similarity = StringUtils.FastMatch(remaining, matchText);
                    if (similarity < 1)
                    {
                        continue;
                    }
                }

                // Save results.
                matches.Add(new Match
                {
                    Id = idToken.GetValue(),
                    Similarity = similarity,
                    SortTitle = title1 + " " + title2
                });
            }

            var all = new List<long>(matches.Count);
            foreach (Match match in matches)
            {
                all.Add(match.Id);
            }

            return true;
        });

        matches.Sort(delegate (Match x, Match y)
        {
            return y.Similarity - x.Similarity;
        });

        List<long> ids = new(matches.Count);
        foreach (Match match in matches)
        {
            ids.Add(match.Id);
        }

        return ids;
    }

    private class Match
    {
        public long Id;
        public int Similarity = 0;
        public string SortTitle = "";
    }
}
