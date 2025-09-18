// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Specialized;
using System.Web;

namespace ComicReader.Common.Actions;

internal class ActionModel
{
    public string Name { get; }
    public NameValueCollection Parameters { get; }

    private ActionModel(string name, NameValueCollection parameters)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public override string ToString()
    {
        var uriBuilder = new UriBuilder
        {
            Scheme = ActionHandler.ACTION_SCHEME,
            Host = Name,
            Query = Parameters.ToString(),
        };
        return uriBuilder.ToString();
    }

    public class Builder
    {
        private string Host { get; }
        private NameValueCollection Query { get; } = HttpUtility.ParseQueryString(string.Empty);

        private Builder(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host cannot be null or whitespace.", nameof(host));
            }

            Host = host;
        }

        public static Builder Create(string host)
        {
            return new Builder(host);
        }

        public Builder AddParameter(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
            }

            Query[key] = value ?? string.Empty;
            return this;
        }

        public ActionModel Build()
        {
            return new ActionModel(Host, Query);
        }
    }
}
