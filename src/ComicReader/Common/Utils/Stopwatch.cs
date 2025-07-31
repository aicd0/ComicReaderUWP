// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Utils;

public class Stopwatch
{
    bool m_reset = true;
    bool m_stoped = true;
    private DateTimeOffset m_start_time = DateTimeOffset.MinValue;
    private DateTimeOffset m_lap_time = DateTimeOffset.MinValue;
    private DateTimeOffset m_stop_time = DateTimeOffset.MinValue;

    public Stopwatch()
    {
        Reset();
    }

    public void Start()
    {
        if (m_reset)
        {
            m_start_time = DateTimeOffset.Now;
            m_lap_time = m_start_time;
        }
        else if (m_stoped)
        {
            TimeSpan interval = DateTimeOffset.Now - m_stop_time;
            m_start_time += interval;
            m_lap_time += interval;
        }

        m_reset = false;
        m_stoped = false;
    }

    public void Stop()
    {
        if (!m_stoped)
        {
            m_stop_time = DateTimeOffset.Now;
        }

        m_stoped = true;
    }

    public void Reset()
    {
        m_reset = true;
    }

    public TimeSpan Span()
    {
        if (m_reset)
        {
            return TimeSpan.Zero;
        }

        if (m_stoped)
        {
            return m_stop_time - m_start_time;
        }

        return DateTimeOffset.Now - m_start_time;
    }

    public void Lap()
    {
        if (m_reset)
        {
            return;
        }

        if (m_stoped)
        {
            m_lap_time = m_stop_time;
        }
        else
        {
            m_lap_time = DateTimeOffset.Now;
        }
    }

    public TimeSpan LapSpan()
    {
        if (m_reset)
        {
            return TimeSpan.Zero;
        }

        if (m_stoped)
        {
            return m_stop_time - m_lap_time;
        }

        return DateTimeOffset.Now - m_lap_time;
    }
}
