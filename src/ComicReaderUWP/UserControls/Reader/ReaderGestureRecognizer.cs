// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Input;

namespace ComicReaderUWP.UserControls.Reader;

internal class ReaderGestureRecognizer
{
    private const string TAG = nameof(ReaderGestureRecognizer);

    private readonly GestureRecognizer _gestureRecognizer = new();
    private WeakReference<IHandler> _handler;

    public ReaderGestureRecognizer()
    {
        _gestureRecognizer.GestureSettings =
            GestureSettings.Tap |
            GestureSettings.DoubleTap |
            GestureSettings.ManipulationTranslateX |
            GestureSettings.ManipulationTranslateY |
            GestureSettings.ManipulationTranslateInertia |
            GestureSettings.ManipulationScale;

        _gestureRecognizer.InertiaTranslationDeceleration = 0.002F;

        _gestureRecognizer.Tapped += Handler.Tapped;
        _gestureRecognizer.ManipulationStarted += Handler.ManipulationStarted;
        _gestureRecognizer.ManipulationUpdated += Handler.ManipulationUpdated;
        _gestureRecognizer.ManipulationCompleted += Handler.ManipulationCompleted;
    }

    public bool AutoProcessInertia
    {
        get
        {
            return _gestureRecognizer.AutoProcessInertia;
        }
        set
        {
            _gestureRecognizer.AutoProcessInertia = value;
        }
    }

    private IHandler Handler
    {
        get
        {
            return _handler?.Get() ?? EmptyHandler.Instance;
        }
    }

    public void SetHandler(IHandler handler)
    {
        _handler = new WeakReference<IHandler>(handler);
    }

    public void ProcessDownEvent(PointerPoint value)
    {
        try
        {
            _gestureRecognizer.ProcessDownEvent(value);
        }
        catch (ArgumentException ex)
        {
            Logger.E(TAG, "ProcessDownEvent", ex);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "ProcessDownEvent", ex);
        }
    }

    public void ProcessMoveEvents(IList<PointerPoint> value)
    {
        try
        {
            _gestureRecognizer.ProcessMoveEvents(value);
        }
        catch (ArgumentException ex)
        {
            Logger.E(TAG, "ProcessMoveEvents", ex);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "ProcessMoveEvents", ex);
        }
    }

    public void ProcessUpEvent(PointerPoint value)
    {
        try
        {
            _gestureRecognizer.ProcessUpEvent(value);
        }
        catch (ArgumentException ex)
        {
            Logger.E(TAG, "ProcessUpEvent", ex);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "ProcessUpEvent", ex);
        }
    }

    public void CompleteGesture()
    {
        try
        {
            _gestureRecognizer.CompleteGesture();
        }
        catch (ArgumentException ex)
        {
            Logger.E(TAG, "CompleteGesture", ex);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "CompleteGesture", ex);
        }
    }

    public interface IHandler
    {
        void Tapped(object sender, TappedEventArgs e);

        void ManipulationStarted(object sender, ManipulationStartedEventArgs e);

        void ManipulationUpdated(object sender, ManipulationUpdatedEventArgs e);

        void ManipulationCompleted(object sender, ManipulationCompletedEventArgs e);
    }

    private class EmptyHandler : IHandler
    {
        private EmptyHandler() { }

        public static readonly EmptyHandler Instance = new();

        public void ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
        }

        public void ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
        }

        public void ManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
        }

        public void Tapped(object sender, TappedEventArgs e)
        {
        }
    }
}
