// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;

namespace ComicReaderUWP.Core.Database.Misc;

public abstract class JsonDatabase<T>(string fileName) where T : class
{
    private const string TAG = nameof(JsonDatabase<T>);

    private readonly string _fileName = fileName;
    private readonly ReaderWriterLock _lock = new();
    private T? _jsonModel;
    private readonly ITaskDispatcher _queue = TaskDispatcher.Factory.NewQueue($"{nameof(JsonDatabase<T>)}#{fileName}");

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = DebugUtils.DebugMode,
        Encoder = DebugUtils.DebugMode ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping : System.Text.Encodings.Web.JavaScriptEncoder.Default,
    };

    protected abstract T InitializeModel(T? model);

    protected void Read(Action<T> action)
    {
        T jsonModel = Initialize();
        _lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            action(jsonModel);
        }
        finally
        {
            _lock.ReleaseReaderLock();
        }
    }

    protected R Read<R>(Func<T, R> func)
    {
        T jsonModel = Initialize();
        _lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            return func(jsonModel);
        }
        finally
        {
            _lock.ReleaseReaderLock();
        }
    }

    protected void Write(Action<T> action)
    {
        T jsonModel = Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            action(jsonModel);
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }

    protected R Write<R>(Func<T, R> func)
    {
        T jsonModel = Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            return func(jsonModel);
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }

    protected void Write(T model)
    {
        ArgumentNullException.ThrowIfNull(model, nameof(model));

        Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            _jsonModel = model;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }

    protected void Save()
    {
        string json = Read(model => JsonSerializer.Serialize(model, _serializerOptions));
        _queue.Submit("Save", () =>
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

        _lock.AcquireWriterLock(Timeout.Infinite);
        try
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
                    jsonModel = JsonSerializer.Deserialize<T>(json, _serializerOptions);
                }
                catch (JsonException ex)
                {
                    Logger.F(TAG, nameof(Initialize), ex);
                }
            }

            bool needWrite = jsonModel is null;
            jsonModel = InitializeModel(jsonModel);
            _jsonModel = jsonModel;

            if (needWrite)
            {
                json = JsonSerializer.Serialize(jsonModel, _serializerOptions);
                _queue.Submit("Save", () =>
                {
                    SimpleConfigDatabase.Instance.TryPutConfig(_fileName, json);
                });
            }

            return jsonModel;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }
}
