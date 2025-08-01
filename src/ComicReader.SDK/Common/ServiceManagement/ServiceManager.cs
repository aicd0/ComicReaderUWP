// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace ComicReader.SDK.Common.ServiceManagement;

public static class ServiceManager
{
    private static readonly ConcurrentDictionary<Type, IService> _services = [];

    public static void RegisterService<T>(T service) where T : IService
    {
        ArgumentNullException.ThrowIfNull(service, nameof(service));
        if (!_services.TryAdd(typeof(T), service))
        {
            throw new InvalidOperationException($"Service of type {typeof(T).FullName} is already registered.");
        }
    }

    internal static T GetService<T>() where T : IService
    {
        ArgumentNullException.ThrowIfNull(typeof(T), nameof(T));
        if (_services.TryGetValue(typeof(T), out IService? service))
        {
            return (T)service;
        }
        throw new KeyNotFoundException($"Service of type {typeof(T).FullName} not found.");
    }

    internal static T? GetServiceNullable<T>() where T : IService
    {
        ArgumentNullException.ThrowIfNull(typeof(T), nameof(T));
        if (_services.TryGetValue(typeof(T), out IService? service))
        {
            return (T)service;
        }
        return default;
    }
}
