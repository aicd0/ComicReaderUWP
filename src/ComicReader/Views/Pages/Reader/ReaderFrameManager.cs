// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

namespace ComicReader.Views.Pages.Reader;

internal class ReaderFrameManager
{
    //
    // Constants
    //

    private const string TAG = "ReaderFrameManager";

    //
    // Variables
    //

    private int _readyFrameIndex = -1;
    private readonly Dictionary<int, FrameInfo> _frameInfoDictionary = [];

    public delegate void FrameReadyHandler(int index);
    private FrameReadyHandler _frameReadyHandler;

    //
    // Methods
    //

    public FrameworkElement GetContainer(int index)
    {
        if (_frameInfoDictionary.TryGetValue(index, out FrameInfo frameInfo))
        {
            if (frameInfo.IsReady)
            {
                return frameInfo.Container;
            }
        }

        return null;
    }

    public void ResetReadyIndex()
    {
        _readyFrameIndex = -1;
        _frameInfoDictionary.Clear();
    }

    public void SetFrameReadyHandler(FrameReadyHandler handler)
    {
        _frameReadyHandler = handler;
    }

    public void MarkModelInstanceOutOfDate(int index, string reason)
    {
        FrameInfo frameInfo = GetFrame(index);
        frameInfo.IsModelInstanceUpdateToDate = false;
        OnFrameUpdated(index, frameInfo, reason);
    }

    public void MarkModelInstanceUpdateToDate(int index, string reason)
    {
        FrameInfo frameInfo = GetFrame(index);
        frameInfo.IsModelInstanceUpdateToDate = true;
        OnFrameUpdated(index, frameInfo, reason);
    }

    public void MarkModelContentUpdateToDate(int index, string reason)
    {
        FrameInfo frameInfo = GetFrame(index);
        frameInfo.IsModelContentUpdateToDate = true;
        OnFrameUpdated(index, frameInfo, reason);
    }

    public void MarkViewReady(int index, FrameworkElement container, string reason)
    {
        FrameInfo frameInfo = GetFrame(index);
        frameInfo.IsViewReady = true;
        frameInfo.Container = container;
        OnFrameUpdated(index, frameInfo, reason);
    }

    public void MarkViewNotReady(int index, string reason)
    {
        FrameInfo frameInfo = GetFrame(index);
        frameInfo.IsViewReady = false;
        OnFrameUpdated(index, frameInfo, reason);
    }

    private FrameInfo GetFrame(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!_frameInfoDictionary.TryGetValue(index, out FrameInfo frameInfo))
        {
            frameInfo = new FrameInfo();
            _frameInfoDictionary.Add(index, frameInfo);
        }

        return frameInfo;
    }

    private void OnFrameUpdated(int index, FrameInfo frameInfo, string reason)
    {
        bool oldReady = frameInfo.IsReady;
        bool newReady = frameInfo.IsViewReady && frameInfo.IsModelInstanceUpdateToDate && frameInfo.IsModelContentUpdateToDate;

        if (oldReady == newReady)
        {
            return;
        }
        frameInfo.IsReady = newReady;

        if (newReady)
        {
            IncreaseReadyIndex();
        }
        else
        {
            DecreaseReadyIndex(index);
        }
    }

    private void IncreaseReadyIndex()
    {
        int readyFrameIndex = _readyFrameIndex + 1;
        for (; true; ++readyFrameIndex)
        {
            if (_frameInfoDictionary.TryGetValue(readyFrameIndex, out FrameInfo frameInfo))
            {
                if (!frameInfo.IsReady)
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        --readyFrameIndex;

        if (readyFrameIndex == _readyFrameIndex)
        {
            return;
        }

        int oldFrameIndex = _readyFrameIndex;
        _readyFrameIndex = readyFrameIndex;

        for (int i = oldFrameIndex + 1; i <= readyFrameIndex; ++i)
        {
            if (_frameReadyHandler == null)
            {
                break;
            }
            _frameReadyHandler.Invoke(i);
        }
    }

    private void DecreaseReadyIndex(int index)
    {
        _readyFrameIndex = Math.Min(index - 1, _readyFrameIndex);
    }

    //
    // Structs
    //

    private class FrameInfo
    {
        public bool IsViewReady { get; set; } = false;
        public bool IsModelInstanceUpdateToDate { get; set; } = true;
        public bool IsModelContentUpdateToDate { get; set; } = false;
        public FrameworkElement Container { get; set; }

        public bool IsReady { get; set; } = false;
    }
}
