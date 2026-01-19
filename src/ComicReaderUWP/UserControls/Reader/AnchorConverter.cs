// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ComicReaderUWP.UserControls.Reader;

internal class AnchorConverter
{
    private readonly List<Anchor> _anchors = new(16);
    private bool _sorted = true;

    public int Count => _anchors.Count;

    public void Insert(double page, double offset)
    {
        _sorted = false;
        _anchors.Add(new()
        {
            Page = page,
            Offset = offset,
        });
    }

    public double CalculateOffset(double page)
    {
        if (_anchors.Count == 0)
        {
            throw new InvalidOperationException("Anchors list is empty.");
        }

        Sort();

        if (page <= _anchors[0].Page)
        {
            return _anchors[0].Offset;
        }

        int last = _anchors.Count - 1;
        if (page >= _anchors[last].Page)
        {
            return _anchors[last].Offset;
        }

        int lo = 0;
        int hi = last;
        while (lo + 1 < hi)
        {
            int mid = (lo + hi) / 2;
            if (_anchors[mid].Page <= page)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        Anchor a = _anchors[lo];
        Anchor b = _anchors[hi];
        double diff = b.Page - a.Page;
        if (diff < 1E-8)
        {
            return a.Offset;
        }

        double t = (page - a.Page) / diff;
        return a.Offset + t * (b.Offset - a.Offset);
    }

    public double CalculatePage(double offset)
    {
        if (_anchors.Count == 0)
        {
            throw new InvalidOperationException("Anchors list is empty.");
        }

        Sort();

        if (offset <= _anchors[0].Offset)
        {
            return _anchors[0].Page;
        }

        int last = _anchors.Count - 1;
        if (offset >= _anchors[last].Offset)
        {
            return _anchors[last].Page;
        }

        int lo = 0;
        int hi = last;
        while (lo + 1 < hi)
        {
            int mid = (lo + hi) / 2;
            if (_anchors[mid].Offset <= offset)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        Anchor a = _anchors[lo];
        Anchor b = _anchors[hi];
        double diff = b.Offset - a.Offset;
        if (diff < 1E-8)
        {
            return a.Page;
        }

        double t = (offset - a.Offset) / diff;
        return a.Page + t * (b.Page - a.Page);
    }

    private void Sort()
    {
        if (_sorted)
        {
            return;
        }

        _anchors.Sort((a, b) => a.Page.CompareTo(b.Page));
        _sorted = true;
    }

    private struct Anchor
    {
        public double Page;
        public double Offset;

        public override readonly string ToString()
        {
            return $"P={Page}, O={Offset}";
        }
    }
}
