// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle.Utils;

public static class ObserverUtils
{
    public static IValueObserver<T> Create<T>(Action<T> action)
    {
        return new Observer<T>(action);
    }

    private class Observer<U>(Action<U> action) : IValueObserver<U>
    {
        private readonly Action<U> _action = action;

        public void OnChanged(U value)
        {
            _action(value);
        }
    }
}
