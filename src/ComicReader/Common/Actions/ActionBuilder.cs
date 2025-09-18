// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Specialized;
using System.Web;

namespace ComicReader.Common.Actions;

internal class ActionBuilder
{
    private string Host { get; }
    private NameValueCollection Query { get; } = HttpUtility.ParseQueryString(string.Empty);

    private ActionBuilder(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host cannot be null or whitespace.", nameof(host));
        }

        Host = host;
    }

    public static ActionBuilder Create(string host)
    {
        return new ActionBuilder(host);
    }

    public ActionBuilder AddParameter(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        Query[key] = value ?? string.Empty;
        return this;
    }

    public string Build()
    {
        var uriBuilder = new UriBuilder
        {
            Scheme = ActionHandler.ACTION_SCHEME,
            Host = Host,
            Query = Query.ToString(),
        };
        return uriBuilder.ToString();
    }
}
