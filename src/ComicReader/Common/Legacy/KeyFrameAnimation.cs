// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Threading;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common.Legacy;

internal class KeyFrameAnimation
{
    private const int MAX_FPS = 120;

    private readonly List<KeyFrame> _keyFrames = new();
    private int _currentAnimator = 0;

    public double Duration { get; set; } = 1.0;

    public double StartValue { get; set; } = 0.0;

    public Action<double> UpdateCallback { get; set; }

    public Action StopCallback { get; set; }

    public void InsertKeyFrame(double time, double value, CurveType curve = CurveType.Linear)
    {
        Logger.Assert(time >= 0 && time <= 1, "7C462358FFEB459D");
        time = Math.Max(0, Math.Min(1, time));
        _keyFrames.Add(new KeyFrame
        {
            Time = time,
            Value = value,
            Curve = curve,
        });
    }

    public void RemoveAllKeyFrames()
    {
        _keyFrames.Clear();
    }

    public void Start()
    {
        var keyFrames = _keyFrames.OrderBy(delegate (KeyFrame keyFrame)
        {
            return keyFrame.Time;
        }).ToList();
        double startValue = StartValue;
        double duration = Duration;
        int animator = Interlocked.Increment(ref _currentAnimator);
        _ = MainThreadUtils.RunInMainThreadAsync(async delegate
        {
            long startTick = DateTime.Now.Ticks;
            double startTime = 0.0;
            bool callbackInvoked = true;
            UpdateCallback?.Invoke(startValue);
            foreach (KeyFrame keyFrame in keyFrames)
            {
                while (true)
                {
                    if (callbackInvoked)
                    {
                        await Task.Delay(1000 / MAX_FPS);
                        if (animator != _currentAnimator)
                        {
                            return;
                        }

                        callbackInvoked = false;
                    }

                    long tickElapsed = DateTime.Now.Ticks - startTick;
                    double timeElapsed = tickElapsed / 10000000.0 / duration;
                    if (timeElapsed > keyFrame.Time)
                    {
                        break;
                    }

                    double time = (timeElapsed - startTime) / (keyFrame.Time - startTime);
                    double value = GetValue(time, keyFrame.Curve) * (keyFrame.Value - startValue) + startValue;
                    UpdateCallback?.Invoke(value);
                    callbackInvoked = true;
                }

                startValue = keyFrame.Value;
                startTime = keyFrame.Time;
            }

            if (keyFrames.Count > 0)
            {
                KeyFrame lastFrame = keyFrames[keyFrames.Count - 1];
                UpdateCallback?.Invoke(lastFrame.Value);
            }

            StopCallback?.Invoke();
        });
    }

    public void Stop()
    {
        Interlocked.Increment(ref _currentAnimator);
    }

    private static double GetValue(double time, CurveType curve)
    {
        double value = curve switch
        {
            CurveType.Linear => time,
            _ => throw new ArgumentException(),
        };
        return value;
    }

    private class KeyFrame
    {
        public double Time { get; set; }

        public double Value { get; set; }

        public CurveType Curve { get; set; }
    }

    public enum CurveType
    {
        Linear,
    }
}
