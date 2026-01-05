// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Database.Misc;

public abstract class JsonDatabase<T>(string fileName) where T : class
{
    private const string TAG = nameof(JsonDatabase<T>);

    private readonly string _fileName = fileName;
    private readonly ReaderWriterLock _lock = new();
    private T _jsonModel;
    private readonly ITaskDispatcher _queue = TaskDispatcher.Factory.NewQueue($"{nameof(JsonDatabase<T>)}#{fileName}");

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = DebugUtils.DebugMode,
        Encoder = DebugUtils.DebugMode ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping : System.Text.Encodings.Web.JavaScriptEncoder.Default,
    };

    protected abstract T CreateModel();

    protected R Read<R>(Func<T, R> func)
    {
        Initialize();
        _lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            return func(_jsonModel);
        }
        finally
        {
            _lock.ReleaseReaderLock();
        }
    }

    protected R Write<R>(Func<T, R> func)
    {
        Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            return func(_jsonModel);
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
        T clonedModel = Read(CloneModel);
        if (clonedModel != null)
        {
            _queue.Submit("Save", () =>
            {
                string json = JsonSerializer.Serialize(clonedModel, _serializerOptions);
                SimpleConfigDatabase.Instance.TryPutConfig(_fileName, json);
            });
        }
    }

    protected T CloneModel(T model)
    {
        string json = JsonSerializer.Serialize(model, _serializerOptions);
        try
        {
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }
        catch (JsonException ex)
        {
            Logger.F(TAG, nameof(CloneModel), ex);
            return null;
        }
    }

    private void Initialize()
    {
        if (_jsonModel != null)
        {
            return;
        }

        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            if (_jsonModel != null)
            {
                return;
            }

            string json = SimpleConfigDatabase.Instance.TryGetConfig(_fileName);
            T jsonModel = null;
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

            jsonModel ??= CreateModel();
            _jsonModel = jsonModel;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }
}
