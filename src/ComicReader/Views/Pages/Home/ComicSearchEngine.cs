// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Expression;
using ComicReader.Common.Expression.Sql;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.Views.Pages.Search;

namespace ComicReader.Views.Pages.Home;

internal class ComicSearchEngine
{
    private const string TAG = nameof(ComicSearchEngine);

    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.DefaultQueue;
    private int _updateSubmitted = 0;

    private Action<IReadOnlyList<ComicModel>>? _callback = null;
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
        _callback = callback;
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

        _dispatcher.Submit("UpdateLibrary", delegate
        {
            Interlocked.Exchange(ref _updateSubmitted, 0);
            UpdateNoLock().Wait();
        });
    }

    private async Task UpdateNoLock()
    {
        Logger.I(TAG, "UpdateNoLock");

        string searchText = _searchText;
        string expression = _expression;
        ICondition? expressionCondition = ParseExpression(expression);

        List<long> ids;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ids = await SearchAll(expressionCondition);
        }
        else
        {
            ids = await SearchByKeywords(searchText, expressionCondition);
        }
        List<ComicModel> comicItems = await ComicModel.BatchFromId("HomeLoadComic", ids);

        _comicItems.Clear();
        _comicItems.AddRange(comicItems);
        _callback?.Invoke(comicItems);
    }

    private ICondition? ParseExpression(string expression)
    {
        ExpressionToken token;
        try
        {
            token = ExpressionParser.Parse(expression);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }
        ICondition condition;
        try
        {
            condition = SQLGenerator.CreateQuery(token, new ComicSQLCommandProvider());
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }
        return condition;
    }

    private async Task<List<long>> SearchAll(ICondition? additionalCondition)
    {
        List<long> ids = [];
        await ComicData.Enqueue("HomeLoadLibrary", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            command.AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false)));
            if (additionalCondition is not null)
            {
                command.AppendCondition(additionalCondition);
            }
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                ids.Add(idToken.GetValue());
            }

            return true;
        });

        return ids;
    }

    private async Task<List<long>> SearchByKeywords(string searchText, ICondition? additionalCondition)
    {
        string keyword = searchText;

        // Extract filters and keywords from string.
        var filter = Filter.Parse(keyword, out List<string> remaining);
        var keywords = new List<string>();

        foreach (string text in remaining)
        {
            keywords = keywords.Concat(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        }

        if (!filter.ContainsFilter("hidden"))
        {
            _ = filter.AddFilter("~hidden");
        }

        string title_text;
        string tab_title;
        string filter_brief = filter.DescriptionBrief();
        string filter_details = filter.DescriptionDetailed();

        if (keywords.Count != 0)
        {
            string keyword_combined = StringUtils.Join(" ", keywords);
            title_text = "\"" + keyword_combined + "\"";
            tab_title = StringResourceProvider.Instance.SearchResultsOf;
            tab_title = tab_title.Replace("$keyword", keyword_combined);
        }
        else if (filter_brief.Length != 0)
        {
            title_text = filter_brief;
            tab_title = filter_brief;
            filter_details = "";
        }
        else
        {
            title_text = StringResourceProvider.Instance.AllMatchedResults;
            tab_title = StringResourceProvider.Instance.SearchResults;
        }

        for (int i = 0; i < keywords.Count; ++i)
        {
            keywords[i] = keywords[i].ToLower();
        }

        var keyword_matched = new List<Match>();
        List<long> filter_matched = [];

        await ComicData.Enqueue("SearchComics", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            IReaderToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            IReaderToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
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

                if (keywords.Count != 0)
                {
                    string match_text = title1 + " " + title2;
                    similarity = StringUtils.QuickMatch(keywords, match_text);

                    if (similarity < 1)
                    {
                        continue;
                    }
                }

                // Save results.
                keyword_matched.Add(new Match
                {
                    Id = idToken.GetValue(),
                    Similarity = similarity,
                    SortTitle = title1 + " " + title2
                });
            }

            var all = new List<long>(keyword_matched.Count);

            foreach (Match match in keyword_matched)
            {
                all.Add(match.Id);
            }

            filter_matched = filter.Match(all);
            return true;
        });

        // Intersect two.
        var matches = C3<Match, long, long>.Intersect(keyword_matched, filter_matched,
            (Match x) => x.Id, (long x) => x,
            new C1<long>.DefaultEqualityComparer()).ToList();

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
