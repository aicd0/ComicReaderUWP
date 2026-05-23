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

        _gestureRecognizer.Tapped += delegate (GestureRecognizer sender, TappedEventArgs e)
        {
            Handler.Tapped(sender, e);
        };
        _gestureRecognizer.ManipulationStarted += delegate (GestureRecognizer sender, ManipulationStartedEventArgs e)
        {
            Handler.ManipulationStarted(sender, e);
        };
        _gestureRecognizer.ManipulationUpdated += delegate (GestureRecognizer sender, ManipulationUpdatedEventArgs e)
        {
            Handler.ManipulationUpdated(sender, e);
        };
        _gestureRecognizer.ManipulationCompleted += delegate (GestureRecognizer sender, ManipulationCompletedEventArgs e)
        {
            Handler.ManipulationCompleted(sender, e);
        };
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
        catch (Exception e)
        {
            Logger.F(TAG, "ProcessDownEvent", e);
        }
    }

    public void ProcessMoveEvents(IList<PointerPoint> value)
    {
        try
        {
            _gestureRecognizer.ProcessMoveEvents(value);
        }
        catch (ArgumentException e)
        {
            Logger.E(TAG, "ProcessMoveEvents", e);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "ProcessMoveEvents", e);
        }
    }

    public void ProcessUpEvent(PointerPoint value)
    {
        try
        {
            _gestureRecognizer.ProcessUpEvent(value);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "ProcessUpEvent", e);
        }
    }

    public void CompleteGesture()
    {
        try
        {
            _gestureRecognizer.CompleteGesture();
        }
        catch (Exception e)
        {
            Logger.F(TAG, "CompleteGesture", e);
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
