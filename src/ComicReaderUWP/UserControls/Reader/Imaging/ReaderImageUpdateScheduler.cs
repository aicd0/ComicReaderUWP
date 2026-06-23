// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class ReaderImageUpdateScheduler
{
    private const string TAG = nameof(ReaderImageUpdateScheduler);

    public static ReaderImageUpdateScheduler Instance { get; } = new();

    private readonly Dictionary<int, CompositionGroupModel> _groups = [];
    private readonly Dictionary<int, int> _lastFrameIndices = [];
    private readonly Stopwatch _stopwatch = new();
    private DispatcherQueueTimer? _timer;

    private ReaderImageUpdateScheduler()
    {
    }

    public void AddGroup(CompositionGroupModel group)
    {
        if (_groups.TryAdd(group.Id, group))
        {
            Logger.I(TAG, $"Add group (i={group.Id})");
            DrawGroup(group);
            EnsureTimerRunning();
        }
    }

    public void RemoveGroup(CompositionGroupModel group)
    {
        Logger.I(TAG, $"Remove group (i={group.Id})");
        _groups.Remove(group.Id);

        // Remove any cached frame indices for items belonging to this group
        foreach (CompositionItemModel item in group.Items)
        {
            _lastFrameIndices.Remove(item.Id);
        }

        if (_groups.Count == 0)
        {
            StopTimer();
        }
    }

    private void EnsureTimerRunning()
    {
        // Create timer lazily on the UI thread
        if (_timer is null)
        {
            var dq = DispatcherQueue.GetForCurrentThread();
            if (dq is null)
            {
                return;
            }

            _timer = dq.CreateTimer();
            _timer.Tick += Timer_Tick;
        }

        // Start stopwatch and timer if there are animated frames
        if (!_timer.IsRunning && HasAnimatedContent())
        {
            Logger.I(TAG, $"Timer start");
            _stopwatch.Restart();
            _timer.Interval = TimeSpan.FromMilliseconds(16); // initial baseline
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        if (_timer is not null && _timer.IsRunning)
        {
            Logger.I(TAG, $"Timer stop");
            _timer.Stop();
        }

        _stopwatch.Reset();
    }

    private bool HasAnimatedContent()
    {
        foreach (CompositionGroupModel g in _groups.Values)
        {
            foreach (CompositionItemModel item in g.Items)
            {
                if (item.BitmapRef.TryRef(out AnimatedBitmapModel? bm))
                {
                    try
                    {
                        if (bm.FrameCount > 1)
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        item.BitmapRef.Unref();
                    }
                }
            }
        }

        return false;
    }

    private void Timer_Tick(DispatcherQueueTimer? sender, object? args)
    {
        if (_stopwatch is null)
        {
            return;
        }

        long elapsedMs = _stopwatch.ElapsedMilliseconds;

        long nextChangeMs = long.MaxValue;
        List<CompositionGroupModel> dirtyGroups = [];

        foreach (CompositionGroupModel group in _groups.Values)
        {
            bool needDraw = false;

            foreach (CompositionItemModel item in group.Items)
            {
                if (!item.BitmapRef.TryRef(out AnimatedBitmapModel? bm))
                {
                    continue;
                }

                try
                {
                    if (bm.FrameCount <= 1)
                    {
                        continue;
                    }

                    int frameIndex = bm.GetFrameIndexAtTime(elapsedMs);
                    if (!_lastFrameIndices.TryGetValue(item.Id, out int last) || last != frameIndex)
                    {
                        needDraw = true;
                    }

                    // compute time until next frame change
                    long duration = bm.Duration;
                    long t = duration == 0 ? 0 : ((elapsedMs % duration) + duration) % duration;
                    long nextStart = (frameIndex + 1 < bm.FrameCount) ? bm.GetFrameStartTime(frameIndex + 1) : duration;
                    long rem = nextStart - t;
                    if (rem < 0)
                    {
                        rem = 0;
                    }
                    if (rem < nextChangeMs)
                    {
                        nextChangeMs = rem;
                    }
                }
                finally
                {
                    item.BitmapRef.Unref();
                }
            }

            if (needDraw)
            {
                dirtyGroups.Add(group);
            }
        }

        // Redraw dirty groups
        foreach (CompositionGroupModel g in dirtyGroups)
        {
            DrawGroup(g);
        }

        if (nextChangeMs == long.MaxValue)
        {
            // No animated frames left
            StopTimer();
            return;
        }

        // Schedule next tick; convert ms to TimeSpan and ensure minimum resolution
        long intervalMs = Math.Max(1, nextChangeMs);
        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        }
    }

    private void DrawGroup(CompositionGroupModel group)
    {
        if (!group.SurfaceRef.TryRef(out CompositionDrawingSurface? surface))
        {
            return;
        }

        try
        {
            using CanvasDrawingSession ds = CanvasComposition.CreateDrawingSession(surface);
            ds.Clear(Microsoft.UI.Colors.Transparent);

            foreach (CompositionItemModel item in group.Items)
            {
                if (!item.BitmapRef.TryRef(out AnimatedBitmapModel? bitmapModel))
                {
                    continue;
                }

                try
                {
                    // Choose the correct frame based on elapsed time (stopwatch may be running)
                    long elapsedMs = _stopwatch.IsRunning ? _stopwatch.ElapsedMilliseconds : 0;
                    int frameIndex = bitmapModel.GetFrameIndexAtTime(elapsedMs);

                    Matrix3x2 oldTransform = ds.Transform;
                    ds.Transform = item.GetTransformMatrix(item.CanvasRect, out RectangleF destRect);

                    CanvasBitmap frameBitmap = bitmapModel.GetFrameBitmap(frameIndex);
                    ds.DrawImage(
                        CreateCanvasImage(frameBitmap, item.ImageSource),
                        new Windows.Foundation.Rect(destRect.X, destRect.Y, destRect.Width, destRect.Height),
                        new Windows.Foundation.Rect(0, 0, bitmapModel.SizeInPixels.Width, bitmapModel.SizeInPixels.Height),
                        1F,
                        CanvasImageInterpolation.HighQualityCubic);

                    ds.Transform = oldTransform;

                    // Cache last drawn frame for scheduling purposes
                    _lastFrameIndices[item.Id] = frameIndex;
                }
                finally
                {
                    item.BitmapRef.Unref();
                }
            }
        }
        finally
        {
            group.SurfaceRef.Unref();
        }
    }

    private static ICanvasImage CreateCanvasImage(CanvasBitmap bitmap, ReaderImageSource source)
    {
        ICanvasImage result = bitmap;

        float contrast = source.Contrast;
        if (contrast != 0)
        {
            result = new ContrastEffect()
            {
                Source = result,
                Contrast = Math.Clamp(contrast, -1, 1),
            };
        }

        if (source.Invert)
        {
            result = new InvertEffect()
            {
                Source = result,
            };
        }

        float brightness = source.Brightness;
        if (brightness != 0)
        {
            if (float.IsPositive(brightness))
            {
                result = new BrightnessEffect()
                {
                    Source = result,
                    BlackPoint = new Vector2(0, brightness),
                };
            }
            else
            {
                result = new BrightnessEffect()
                {
                    Source = result,
                    WhitePoint = new Vector2(1, brightness + 1F),
                };
            }
        }

        return result;
    }
}
