// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Helpers.Navigation;

public class Route
{
    public static Route Create(string url)
    {
        Uri uri = new(url);
        string scheme = uri.Scheme;
        string host = uri.Host;
        int port = uri.Port;
        string path = uri.AbsolutePath;
        string fragment = uri.Fragment;

        string query = uri.Query;
        if (query.StartsWith('?'))
        {
            query = query[1..];
        }

        string[] queries = query.Split('&');
        var queriesDict = new Dictionary<string, string>();
        foreach (string q in queries)
        {
            if (q.Length == 0)
            {
                continue;
            }

            int index = q.IndexOf('=');
            string key = q[..index];
            string value = q[(index + 1)..];
            queriesDict[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return new Route(scheme, host, port, path, queriesDict, fragment);
    }

    public string Scheme { get; }
    public string Host { get; }
    public int Port { get; }
    public Dictionary<string, string> Queries { get; }
    public string Path { get; }
    public string Fragment { get; }
    public string Url { get => EvaluateUrl(); }

    private string? _url;

    private Route(string scheme, string host, int port, string path, Dictionary<string, string> queries, string fragment)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        Path = path;
        Queries = queries;
        Fragment = fragment;
    }

    public Route WithParam(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.AssertNotReachHere("0D4C54A375DE2353");
            return this;
        }

        if (value == null)
        {
            if (Queries.Remove(key))
            {
                _url = null;
            }
        }
        else
        {
            Queries[key] = value;
            _url = null;
        }

        return this;
    }

    private string EvaluateUrl()
    {
        if (_url != null)
        {
            return _url;
        }

        _url = BuildUrl();
        return _url;
    }

    private string BuildUrl()
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append(Scheme);
        urlBuilder.Append("://");
        urlBuilder.Append(Host);
        if (Port >= 0)
        {
            urlBuilder.Append(':');
            urlBuilder.Append(Port);
        }

        if (Path != "/")
        {
            urlBuilder.Append(Path);
        }

        if (Queries.Count > 0)
        {
            urlBuilder.Append('?');
            bool isFirst = true;
            foreach (KeyValuePair<string, string> query in Queries)
            {
                if (!isFirst)
                {
                    urlBuilder.Append('&');
                }
                isFirst = false;

                urlBuilder.Append(Uri.EscapeDataString(query.Key));
                urlBuilder.Append('=');
                urlBuilder.Append(Uri.EscapeDataString(query.Value));
            }
        }

        urlBuilder.Append(Fragment);
        return urlBuilder.ToString();
    }
}
