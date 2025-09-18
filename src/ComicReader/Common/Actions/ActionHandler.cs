// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common.Actions;

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
        ActionProviderContext providerContext = new(this);
        provider.Handle(providerContext, queires);
        if (providerContext.Successful)
        {
            callback.OnSuccess();
        }
        else
        {
            callback.OnError(providerContext.ErrorMessage);
        }
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

    private class ActionProviderContext(ActionHandler handler) : IActionProviderContext
    {
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
            Successful = false;
            ErrorMessage = message;
        }
    }
}
