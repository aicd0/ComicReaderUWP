// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Linq;

using ComicReader.Common;
using ComicReader.Common.Legacy;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

using LiteDB;

using Microsoft.Data.Sqlite;

namespace ComicReader.Views.Pages.Search;

public class Filter
{
    private readonly List<SubFilter> m_subfilters = new();

    public string DescriptionBrief()
    {
        string unique_string = "";

        foreach (SubFilter filter in m_subfilters)
        {
            unique_string += "<" + filter.UniqueString + ">";
        }

        if (unique_string == "<~hidden>")
        {
            return StringResourceProvider.Instance.AllComics;
        }
        else if (unique_string == "<hidden>")
        {
            return StringResourceProvider.Instance.AllHidden;
        }
        else if (m_subfilters.Count == 2 && m_subfilters[0] is SubFilterDirectory && m_subfilters[1].UniqueString == "~hidden")
        {
            string dir = (m_subfilters[0] as SubFilterDirectory).Directory;
            string format_string = StringResourceProvider.Instance.AllComicsIn;
            format_string = format_string.Replace("$path", dir);
            return format_string;
        }

        return "";
    }

    public string DescriptionDetailed()
    {
        var cpy = new List<SubFilter>(m_subfilters);
        _ = cpy.RemoveAll(x => x.UniqueString == "~hidden");

        if (ContainsFilter("hidden", cpy))
        {
            cpy = m_subfilters;
        }

        if (cpy.Count == 0)
        {
            return "";
        }

        string format_string = StringResourceProvider.Instance.FilteredBy;
        format_string = format_string.Replace("$count", cpy.Count.ToString());
        string res = format_string + ": ";

        foreach (SubFilter f in cpy)
        {
            res += " <" + f.UniqueString + ">";
        }

        return res;
    }

    public bool AddFilter(string desc)
    {
        SubFilter subfilter = ParseFilter(desc);

        if (subfilter == null)
        {
            return false;
        }

        AddFilter(subfilter);
        return true;
    }

    public void AddFilter(SubFilter subfilter)
    {
        m_subfilters.Add(subfilter);
        Optimize();
    }

    public void RemoveFilter(string desc)
    {
        for (int i = m_subfilters.Count - 1; i >= 0; --i)
        {
            if (m_subfilters[i].UniqueString.Equals(desc))
            {
                m_subfilters.RemoveAt(i);
            }
        }

        Optimize();
    }

    public bool ContainsFilter(string desc)
    {
        return ContainsFilter(desc, m_subfilters);
    }

    private static bool ContainsFilter(string desc, List<SubFilter> subfilters)
    {
        foreach (SubFilter f in subfilters)
        {
            if (f.ContainsFilter(desc))
            {
                return true;
            }
        }

        return false;
    }

    public List<long> Match(List<long> all)
    {
        List<long> result = all;

        foreach (SubFilter filter in m_subfilters)
        {
            result = result.Intersect(filter.Match(all)).ToList();
        }

        return result;
    }

    public bool IsEmpty()
    {
        return m_subfilters.Count == 0;
    }

    public static Filter Parse(string desc, out List<string> remaining)
    {
        var filter = new Filter();
        List<SubFilter> filters = ParseFilters(desc, out remaining);

        foreach (SubFilter f in filters)
        {
            filter.AddFilter(f);
        }

        filter.Optimize();
        return filter;
    }

    private void Optimize()
    {
        if (m_subfilters.Count == 0)
        {
            return;
        }

        _ = m_subfilters.RemoveAll(x => x is SubFilterAll);

        if (m_subfilters.Count == 0)
        {
            m_subfilters.Add(new SubFilterAll());
            return;
        }

        m_subfilters.OrderBy((x) => x.UniqueString);
    }

    private static List<SubFilter> ParseFilters(string desc, out List<string> remaining)
    {
        remaining = new List<string>();
        var filters = new List<SubFilter>();
        int bracket_cnt = 0;
        int bracket_start = 0;
        int keyword_start = 0;

        for (int i = 0; i < desc.Length; ++i)
        {
            char c = desc[i];

            if (c == '>')
            {
                bracket_cnt--;

                if (bracket_cnt == 0)
                {
                    string sub_desc = desc.Substring(bracket_start + 1, i - bracket_start - 1);
                    SubFilter filter = ParseFilter(sub_desc);

                    if (filter != null)
                    {
                        filters.Add(filter);

                        if (bracket_start != keyword_start)
                        {
                            remaining.Add(desc.Substring(keyword_start, bracket_start - keyword_start));
                        }

                        keyword_start = i + 1;
                    }
                }

                if (bracket_cnt < 0)
                {
                    bracket_cnt = 0;
                }
            }
            else if (c == '<')
            {
                if (bracket_cnt == 0)
                {
                    bracket_start = i;
                }

                bracket_cnt++;
            }
        }

        if (desc.Length != keyword_start)
        {
            remaining.Add(desc.Substring(keyword_start, desc.Length - keyword_start));
        }

        return filters;
    }

    private static SubFilter ParseFilter(string desc)
    {
        string[] pieces = desc.Split(':', 2);
        string filter_type = pieces[0].Trim().ToLower();

        string args = "";
        if (pieces.Length >= 2)
        {
            args = pieces[1].Trim();
        }

        return ParseFilter(filter_type, args);
    }

    private static SubFilter ParseFilter(string filter_type, string args)
    {
        if (filter_type.Length >= 1 && filter_type[0] == '~')
        {
            SubFilter subfilter = ParseFilter(filter_type.Substring(1), args);

            if (subfilter == null)
            {
                return null;
            }

            return new SubFilterInverse(subfilter);
        }

        if (filter_type == "or" || filter_type == "and")
        {
            List<SubFilter> filters = ParseFilters(args, out List<string> _);

            if (filters.Count == 0)
            {
                return null;
            }

            if (filters.Count == 1)
            {
                return filters[0];
            }

            if (filter_type == "or")
            {
                return new SubFilterOr(filters);
            }
            else
            {
                return new SubFilterAnd(filters);
            }
        }

        {
            List<string> sub_args = [.. args.Split(',')];

            for (int i = sub_args.Count - 1; i >= 0; --i)
            {
                sub_args[i] = sub_args[i].Trim();

                if (sub_args[i].Length == 0)
                {
                    sub_args.RemoveAt(i);
                }
            }

            if (sub_args.Count == 0)
            {
                sub_args.Add("");
            }

            var filters = new List<SubFilter>();
            var all_unique_strings = new List<string>();

            foreach (string arg in sub_args)
            {
                SubFilter filter = ParseFilterRaw(filter_type, arg);

                if (filter == null)
                {
                    continue;
                }

                filters.Add(filter);
                all_unique_strings.Add(arg);
            }

            if (filters.Count == 0)
            {
                return null;
            }

            if (filters.Count == 1)
            {
                return filters[0];
            }

            {
                string unique_string = filter_type + ": " + string.Join(", ", all_unique_strings);
                var filter = new SubFilterOr(filters);
                filter.SetUniqueString(unique_string);
                return filter;
            }
        }
    }

    private static SubFilter ParseFilterRaw(string filter_type, string args)
    {
        return filter_type switch
        {
            "all" => new SubFilterAll(),
            "dir" or "directory" or "folder" or "location" or "path" => new SubFilterDirectory(args),
            "hidden" or "hide" => new SubFilterHidden(),
            "id" or "no" or "num" or "number" => new SubFilterId(args),
            "rating" or "ratings" or "rate" or "star" or "stars" => new SubFilterRating(args),
            "tag" or "tags" => new SubFilterTag(args),
            _ => new SubFilterCategoryTag(filter_type, args),
        };
    }
}

public class SqlParameterContext
{
    private readonly SqliteParameterCollection m_params;
    private int m_index = 0;

    public SqlParameterContext(SqliteParameterCollection parameters)
    {
        m_params = parameters;
    }

    public string AddValue(object value)
    {
        string name = "$" + m_index.ToString();
        m_params.AddWithValue(name, value);
        m_index++;
        return name;
    }
}

// subfilter definitions
public abstract class SubFilter
{
    public abstract string UniqueString { get; }

    public abstract List<long> Match(List<long> all);

    public virtual bool ContainsFilter(string desc)
    {
        return desc == UniqueString;
    }
}

// <~...>
public class SubFilterInverse : SubFilter
{
    private readonly SubFilter m_subfilter;

    public override string UniqueString => "~" + m_subfilter.UniqueString;

    public SubFilterInverse(SubFilter subfilter)
    {
        m_subfilter = subfilter;
    }

    public override List<long> Match(List<long> all)
    {
        return all.Except(m_subfilter.Match(all)).ToList();
    }

    public override bool ContainsFilter(string desc)
    {
        return base.ContainsFilter(desc) || m_subfilter.ContainsFilter(desc);
    }
}

// <or>
public class SubFilterOr : SubFilter
{
    private readonly List<SubFilter> m_filters = new();
    private string m_unique_string = "";

    public override string UniqueString => m_unique_string;

    public SubFilterOr(IEnumerable<SubFilter> filters)
    {
        foreach (SubFilter filter in filters)
        {
            m_filters.Add(filter);
        }

        if (m_filters.Count == 0)
        {
            return;
        }

        var all_unique_strings = new List<string>();

        foreach (SubFilter filter in m_filters)
        {
            all_unique_strings.Add("<" + filter.UniqueString + ">");
        }

        m_unique_string = "or: " + string.Join(", ", all_unique_strings);
    }

    public override List<long> Match(List<long> all)
    {
        var result = new List<long>();

        foreach (SubFilter filter in m_filters)
        {
            result = result.Union(filter.Match(all)).ToList();
        }

        return result;
    }

    public override bool ContainsFilter(string desc)
    {
        foreach (SubFilter filter in m_filters)
        {
            if (filter.ContainsFilter(desc))
            {
                return true;
            }
        }

        return base.ContainsFilter(desc);
    }

    public void SetUniqueString(string val)
    {
        m_unique_string = val;
    }
}

// <and>
public class SubFilterAnd : SubFilter
{
    private readonly List<SubFilter> m_filters = new();
    private readonly string m_unique_string = "";

    public override string UniqueString => m_unique_string;

    public SubFilterAnd(IEnumerable<SubFilter> filters)
    {
        foreach (SubFilter filter in filters)
        {
            m_filters.Add(filter);
        }

        if (m_filters.Count == 0)
        {
            return;
        }

        var all_unique_strings = new List<string>();

        foreach (SubFilter filter in m_filters)
        {
            all_unique_strings.Add("<" + filter.UniqueString + ">");
        }

        m_unique_string = "and: " + string.Join(", ", all_unique_strings);
    }

    public override List<long> Match(List<long> all)
    {
        List<long> result = all;

        foreach (SubFilter filter in m_filters)
        {
            result = result.Intersect(filter.Match(all)).ToList();
        }

        return result;
    }

    public override bool ContainsFilter(string desc)
    {
        foreach (SubFilter filter in m_filters)
        {
            if (filter.ContainsFilter(desc))
            {
                return true;
            }
        }

        return base.ContainsFilter(desc);
    }
}

// <...: ...>
// Tags filter with category specified.
public class SubFilterCategoryTag : SubFilter
{
    private readonly string m_category;
    private readonly string m_tag;

    public override string UniqueString => m_category + ": " + m_tag;

    public SubFilterCategoryTag(string category, string tag)
    {
        m_category = category.ToLower();
        m_tag = tag.ToLower();
    }

    private class MatchedItem
    {
        public long TagCategoryId;
        public long ComicId;
    }

    public override List<long> Match(List<long> all)
    {
        var tag_category_matched = new List<MatchedItem>();
        var tag_matched = new List<long>();

        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(TagCategoryTable.ColumnName).CollateNocase(), ColumnOrValue.FromValue(m_category)));
            IReaderToken<long> tagCateogryIdToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                tag_category_matched.Add(new MatchedItem
                {
                    TagCategoryId = tagCateogryIdToken.GetValue(),
                    ComicId = comicIdToken.GetValue(),
                });
            }
        }

        {
            SelectCommand command = SelectCommand.Create(TagTable.Instance)
                .AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent).CollateNocase(), ColumnOrValue.FromValue(m_tag)));
            IReaderToken<long> categoryIdToken = command.PutQueryInt64(TagTable.ColumnTagCategoryId);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                tag_matched.Add(categoryIdToken.GetValue());
            }
        }

        // Intersect two.
        IEnumerable<MatchedItem> matched = C3<MatchedItem, long, long>.Intersect(tag_category_matched, tag_matched,
            (x) => x.TagCategoryId, (x) => x,
            new C1<long>.DefaultEqualityComparer());

        var results = new List<long>(matched.Count());

        foreach (MatchedItem item in matched)
        {
            results.Add(item.ComicId);
        }

        return results;
    }
}

// <all>
public class SubFilterAll : SubFilter
{
    public override string UniqueString => "all";

    public override List<long> Match(List<long> all)
    {
        return all;
    }
}

// <dir>
public class SubFilterDirectory : SubFilter
{
    private readonly string m_directory;
    public string Directory => m_directory;

    public override string UniqueString => "dir: " + m_directory;

    public SubFilterDirectory(string directory)
    {
        m_directory = directory.ToLower().Replace('/', '\\');
    }

    public override List<long> Match(List<long> all)
    {
        var results = new List<long>();

        SelectCommand command = SelectCommand.Create(ComicTable.Instance)
            .AppendCondition(new LikeCondition(ComicTable.ColumnLocation, m_directory + "%"));
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand.IReader reader = command.Execute();

        while (reader.Read())
        {
            results.Add(comicIdToken.GetValue());
        }

        return results;
    }
}

// <hidden>
public class SubFilterHidden : SubFilter
{
    public override string UniqueString => "hidden";

    public override List<long> Match(List<long> all)
    {
        var results = new List<long>();

        SelectCommand command = SelectCommand.Create(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnHidden, true);
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand.IReader reader = command.Execute();

        while (reader.Read())
        {
            results.Add(comicIdToken.GetValue());
        }

        return results;
    }
}

// <id>
public class SubFilterId : SubFilter
{
    private readonly long m_id;
    private readonly bool m_valid;

    public override string UniqueString => "id: " + m_id.ToString();

    public SubFilterId(string id)
    {
        if (id.Length > 0 && id[0] == '#')
        {
            id = id.Substring(1);
        }

        m_valid = long.TryParse(id, out m_id);
    }

    public override List<long> Match(List<long> all)
    {
        var results = new List<long>();

        SelectCommand command = SelectCommand.Create(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnId, m_id)
            .Limit(1);
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand.IReader reader = command.Execute();

        while (reader.Read())
        {
            results.Add(comicIdToken.GetValue());
        }

        return results;
    }
}

// <rating>
public class SubFilterRating : SubFilter
{
    private readonly int m_rating;
    private readonly bool m_valid;

    public override string UniqueString => "rating: " + m_rating.ToString();

    public SubFilterRating(string rating)
    {
        m_valid = int.TryParse(rating, out m_rating);

        if (m_valid && m_rating < 1)
        {
            m_rating = -1;
        }
    }

    public override List<long> Match(List<long> all)
    {
        var results = new List<long>();

        SelectCommand command = SelectCommand.Create(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnRating, m_rating);
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand.IReader reader = command.Execute();

        while (reader.Read())
        {
            results.Add(comicIdToken.GetValue());
        }

        return results;
    }
}

// <tag>
public class SubFilterTag : SubFilter
{
    private readonly string m_tag;

    public override string UniqueString => "tag: " + m_tag;

    public SubFilterTag(string tag)
    {
        m_tag = tag.ToLower();
    }

    public override List<long> Match(List<long> all)
    {
        var results = new List<long>();

        SelectCommand command = SelectCommand.Create(TagTable.Instance)
            .AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(TagTable.ColumnContent).CollateNocase(), ColumnOrValue.FromValue(m_tag)))
            .Distinct();
        IReaderToken<long> comicIdToken = command.PutQueryInt64(TagTable.ColumnComicId);
        using SelectCommand.IReader reader = command.Execute();

        while (reader.Read())
        {
            results.Add(comicIdToken.GetValue());
        }

        return results;
    }
}
