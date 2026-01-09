// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

using ComicReaderUWP.SDK.Common.DebugTools;

namespace ComicReaderUWP.Common.Actions;

internal class ActionHandler
{
    private const string TAG = nameof(ActionHandler);

    public static ActionHandler Dummy { get; } = new();

    public IActionCallback DefaultCallback { get; set; } = new DefaultActionCallback();

    private readonly ConcurrentDictionary<Type, IActionComponent> _components = [];
    private readonly ConcurrentDictionary<string, IActionProvider> _providers = [];

    public void RegisterComponent<T>(T component) where T : IActionComponent
    {
        ArgumentNullException.ThrowIfNull(component, nameof(component));
        if (!_components.TryAdd(typeof(T), component))
        {
            Logger.E(TAG, $"Failed to register component of type {typeof(T).FullName}. It may already be registered.");
        }
    }

    public void UnregisterComponent<T>() where T : IActionComponent
    {
        ArgumentNullException.ThrowIfNull(typeof(T), nameof(T));
        if (!_components.TryRemove(typeof(T), out _))
        {
            Logger.E(TAG, $"Failed to unregister component of type {typeof(T).FullName}. It may not be registered.");
        }
    }

    public void RegisterProvider(IActionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider, nameof(provider));
        if (!_providers.TryAdd(provider.Name, provider))
        {
            Logger.E(TAG, $"Failed to register provider with key {provider.Name}. It may already be registered.");
        }
    }

    public void UnregisterProvider(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        if (!_providers.TryRemove(key, out _))
        {
            Logger.E(TAG, $"Failed to unregister provider with key {key}. It may not be registered.");
        }
    }

    public bool TryGetComponent<T>([MaybeNullWhen(false)] out T component) where T : IActionComponent
    {
        if (!_components.TryGetValue(typeof(T), out IActionComponent? com))
        {
            component = default;
            return false;
        }

        component = (T)com;
        return true;
    }

    public void Handle(ActionModel action, IActionCallback? callback = null)
    {
        callback ??= DefaultCallback;
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        string host = action.Name;
        if (!_providers.TryGetValue(host, out IActionProvider? provider))
        {
            callback.OnError($"No provider found for action '{host}'.");
            return;
        }

        NameValueCollection queires = action.Parameters;
        ActionProviderContext providerContext = new(this, callback);
        provider.Handle(providerContext, queires);
    }

    private class DefaultActionCallback : IActionCallback
    {
        public void OnSuccess()
        {
        }

        public void OnError(string message)
        {
            Logger.E(TAG, $"Action failed: {message}");
        }
    }

    private class ActionProviderContext(ActionHandler handler, IActionCallback callback) : IActionProviderContext
    {
        public bool Completed { get; private set; } = false;
        public bool Successful { get; private set; } = true;
        public string ErrorMessage { get; private set; } = string.Empty;

        private readonly ActionHandler _handler = handler;

        public T? GetComponent<T>() where T : IActionComponent
        {
            ArgumentNullException.ThrowIfNull(typeof(T), nameof(T));
            if (_handler._components.TryGetValue(typeof(T), out IActionComponent? component))
            {
                return (T)component;
            }

            return default;
        }

        public void SetError(string message)
        {
            if (Completed)
            {
                Logger.F(TAG, "ActionProviderContext is already completed.");
                return;
            }

            Completed = true;
            Successful = false;
            ErrorMessage = message;
            DispatchCallback();
        }

        public void SetSuccess()
        {
            if (Completed)
            {
                Logger.F(TAG, "ActionProviderContext is already completed.");
                return;
            }

            Completed = true;
            Successful = true;
            DispatchCallback();
        }

        private void DispatchCallback()
        {
            if (Successful)
            {
                callback.OnSuccess();
            }
            else
            {
                callback.OnError(ErrorMessage);
            }
        }
    }
}
