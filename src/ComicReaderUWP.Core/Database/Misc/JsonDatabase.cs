// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;

namespace ComicReaderUWP.Core.Database.Misc;

public abstract class JsonDatabase<T>(string fileName) where T : class
{
    private const string TAG = nameof(JsonDatabase<>);

    private static readonly JsonSerializerOptions _saveSerializerOptions = new()
    {
        WriteIndented = DebugUtils.DebugMode,
        Encoder = DebugUtils.DebugMode ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping : System.Text.Encodings.Web.JavaScriptEncoder.Default,
    };

    private static readonly JsonSerializerOptions _cloneSerializerOptions = new();

    private readonly string _fileName = fileName;
    private readonly ITaskDispatcher _queue = TaskDispatcher.Factory.NewQueue($"{nameof(JsonDatabase<>)}#{fileName}");

    private readonly Lock _lock = new();
    private volatile T? _jsonModel;

    protected abstract T InitializeModel(T? model);

    protected void Read(Action<T> action)
    {
        T jsonModel = Initialize();
        action(jsonModel);
    }

    protected R Read<R>(Func<T, R> func)
    {
        T jsonModel = Initialize();
        return func(jsonModel);
    }

    protected void Write(Action<T> action)
    {
        T jsonModel = Initialize();
        lock (_lock)
        {
            T clone = Clone(jsonModel);
            action(clone);
            _jsonModel = clone;
        }
    }

    protected R Write<R>(Func<T, R> func)
    {
        T jsonModel = Initialize();
        lock (_lock)
        {
            T clone = Clone(jsonModel);
            R result = func(clone);
            _jsonModel = clone;
            return result;
        }
    }

    protected void Write(T model)
    {
        ArgumentNullException.ThrowIfNull(model, nameof(model));

        Initialize();
        lock (_lock)
        {
            _jsonModel = model;
        }
    }

    protected void Save()
    {
        string json = Read(model => JsonSerializer.Serialize(model, _saveSerializerOptions));
        _queue.Submit(() =>
        {
            SimpleConfigDatabase.Instance.TryPutConfig(_fileName, json);
        });
    }

    private T Initialize()
    {
        T? jsonModel = _jsonModel;
        if (jsonModel is not null)
        {
            return jsonModel;
        }

        bool needSave;
        lock (_lock)
        {
            jsonModel = _jsonModel;
            if (jsonModel is not null)
            {
                return jsonModel;
            }

            string? json = SimpleConfigDatabase.Instance.TryGetConfig(_fileName);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    jsonModel = JsonSerializer.Deserialize<T>(json, _saveSerializerOptions);
                }
                catch (JsonException ex)
                {
                    Logger.F(TAG, nameof(Initialize), ex);
                }
            }

            needSave = jsonModel is null;
            jsonModel = InitializeModel(jsonModel);
            _jsonModel = jsonModel;
        }

        if (needSave)
        {
            Save();
        }

        return jsonModel;
    }

    private static T Clone(T obj)
    {
        string json = JsonSerializer.Serialize(obj, _cloneSerializerOptions);
        return JsonSerializer.Deserialize<T>(json, _cloneSerializerOptions) ??
            throw new NullReferenceException($"Deserialization of {typeof(T).FullName} produced a null result.");
    }
}
