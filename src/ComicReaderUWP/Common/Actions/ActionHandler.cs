// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Common.Actions;

internal class ActionHandler
{
    private const string TAG = nameof(ActionHandler);

    public static ActionHandler Dummy { get; } = new();

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

    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : IActionComponent
    {
        if (!_components.TryGetValue(typeof(T), out IActionComponent? com))
        {
            component = default;
            return false;
        }

        component = (T)com;
        return true;
    }

    public async Task<ActionResult> Handle(ActionModel action)
    {
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        string host = action.Name;
        if (!_providers.TryGetValue(host, out IActionProvider? provider))
        {
            return ActionResult.FromFailure($"No provider found for action '{host}'.");
        }

        NameValueCollection queires = action.Parameters;
        ActionProviderContext providerContext = new(this);
        return await provider.Handle(providerContext, queires);
    }

    public void HandleNoResult(ActionModel action)
    {
        CoroutineUtils.Run(() => Handle(action));
    }

    private class ActionProviderContext(ActionHandler handler) : IActionProviderContext
    {
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
    }
}
