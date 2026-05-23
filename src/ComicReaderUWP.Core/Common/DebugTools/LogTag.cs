// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReaderUWP.Core.Common.DebugTools;

public class LogTag
{
    private const string EMPTY_TAG = "Empty";

    public static readonly LogTag Empty = new(EMPTY_TAG);

    private readonly TagTree _root;

    private LogTag()
    {
        _root = new TagTree();
    }

    private LogTag(string name)
    {
        _root = new TagTree();
        _root.Add(name);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        var tags = new List<string>();
        _root.AppendToString(false, sb, tags);
        return sb.ToString();
    }

    public bool ContainsAny(LogTag tag)
    {
        if (tag == null)
        {
            return false;
        }
        return _root.ContainsAny(tag._root);
    }

    public static LogTag? FromString(string value)
    {
        var tag = new LogTag();
        string[] pieces = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0)
        {
            return null;
        }

        TagTree current = tag._root;
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i];
            if (i == pieces.Length - 1)
            {
                current.Add(piece);
            }
            else
            {
                current = current.With(piece);
            }
        }

        return tag;
    }

    public static LogTag N(string? name)
    {
        name ??= EMPTY_TAG;
        var tag = new LogTag();
        tag._root.Add(name);
        return tag;
    }

    public static LogTag N(string? name1, string? name2)
    {
        name1 ??= EMPTY_TAG;
        name2 ??= EMPTY_TAG;
        var tag = new LogTag();
        tag._root.With(name1).Add(name2);
        return tag;
    }

    public static LogTag N(string? name1, string? name2, string? name3)
    {
        name1 ??= EMPTY_TAG;
        name2 ??= EMPTY_TAG;
        name3 ??= EMPTY_TAG;
        var tag = new LogTag();
        tag._root.With(name1).With(name2).Add(name3);
        return tag;
    }

    public static LogTag F(LogTag? tag1, LogTag? tag2)
    {
        if (tag1 == null && tag2 == null)
        {
            return Empty;
        }
        var tag = new LogTag();
        if (tag1 != null)
        {
            tag._root.Combine(tag1._root);
        }
        if (tag2 != null)
        {
            tag._root.Combine(tag2._root);
        }
        return tag;
    }

    public static LogTag F(LogTag? tag1, LogTag? tag2, LogTag? tag3)
    {
        if (tag1 == null && tag2 == null && tag3 == null)
        {
            return Empty;
        }
        var tag = new LogTag();
        if (tag1 != null)
        {
            tag._root.Combine(tag1._root);
        }
        if (tag2 != null)
        {
            tag._root.Combine(tag2._root);
        }
        if (tag3 != null)
        {
            tag._root.Combine(tag3._root);
        }
        return tag;
    }

    private class TagTree
    {
        private Dictionary<string, TagTree?>? _subTrees;

        public TagTree() { }

        public TagTree(TagTree tree)
        {
            if (tree._subTrees != null)
            {
                _subTrees = [];
                foreach (KeyValuePair<string, TagTree?> pair in tree._subTrees)
                {
                    TagTree? subTree = null;
                    if (pair.Value != null)
                    {
                        subTree = new TagTree(pair.Value);
                    }
                    _subTrees[pair.Key] = subTree;
                }
            }
        }

        public void Add(string name)
        {
            _subTrees ??= [];
            if (!_subTrees.TryGetValue(name, out TagTree? subTree))
            {
                _subTrees[name] = null;
            }
        }

        public TagTree With(string name)
        {
            _subTrees ??= [];
            if (!_subTrees.TryGetValue(name, out TagTree? subTree) || subTree == null)
            {
                subTree = new TagTree();
                _subTrees[name] = subTree;
            }
            return subTree;
        }

        public void Combine(TagTree tree)
        {
            if (tree._subTrees != null)
            {
                _subTrees ??= [];
                foreach (KeyValuePair<string, TagTree?> pair in tree._subTrees)
                {
                    if (_subTrees.TryGetValue(pair.Key, out TagTree? subTree))
                    {
                        if (pair.Value != null)
                        {
                            if (subTree == null)
                            {
                                subTree = new TagTree(pair.Value);
                                _subTrees[pair.Key] = subTree;
                            }
                            else
                            {
                                subTree.Combine(pair.Value);
                            }
                        }
                        continue;
                    }
                    if (pair.Value != null)
                    {
                        subTree = new TagTree(pair.Value);
                        _subTrees[pair.Key] = subTree;
                    }
                    else
                    {
                        _subTrees[pair.Key] = null;
                    }
                }
            }
        }

        public bool ContainsAny(TagTree? tree)
        {
            if (_subTrees == null)
            {
                return true;
            }

            if (tree == null || tree._subTrees == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, TagTree?> pair in tree._subTrees)
            {
                if (_subTrees.TryGetValue(pair.Key, out TagTree? subTree))
                {
                    if (subTree == null)
                    {
                        return true;
                    }

                    if (subTree.ContainsAny(pair.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void AppendToString(bool divider, StringBuilder sb, List<string> tags)
        {
            if (_subTrees == null)
            {
                return;
            }
            foreach (KeyValuePair<string, TagTree?> pair in _subTrees)
            {
                tags.Add(pair.Key);
                if (pair.Value == null)
                {
                    if (divider)
                    {
                        sb.Append(',');
                    }
                    divider = true;

                    bool tagDivider = false;
                    foreach (string tag in tags)
                    {
                        if (tagDivider)
                        {
                            sb.Append('/');
                        }
                        tagDivider = true;

                        string tagEscaped = tag.Trim();
                        if (tagEscaped.Contains('/'))
                        {
                            tagEscaped = tagEscaped.Replace('/', '_');
                        }
                        if (tagEscaped.Contains(','))
                        {
                            tagEscaped = tagEscaped.Replace(',', '_');
                        }
                        sb.Append(tagEscaped);
                    }
                }
                else
                {
                    pair.Value.AppendToString(divider, sb, tags);
                }
                tags.RemoveAt(tags.Count - 1);
            }
        }
    }
}
