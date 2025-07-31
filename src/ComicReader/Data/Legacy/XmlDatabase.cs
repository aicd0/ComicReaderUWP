// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

using ComicReader.Common.Legacy;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Legacy;

public abstract class XmlData
{
    public abstract string FileName { get; }
    [XmlIgnore]
    public abstract XmlData Target { get; set; }

    public virtual void Pack() { }
    public virtual void Unpack() { }
}

internal class XmlDatabase
{
    public static SettingData Settings = new();
    public static FavoriteData Favorites = new();
    public static HistoryData History = new();
};

internal enum XmlDatabaseItem
{
    Favorites,
    History,
    Settings
}

internal class XmlDatabaseManager
{
    private const string TAG = "XmlDatabaseManager";

    private static string DatabaseFolderPath => StorageLocation.GetLocalFolderPath();

    private static bool m_database_ready = false;
    private static readonly SemaphoreSlim m_database_lock = new(1);

    public static async Task WaitLock()
    {
        await C0.WaitFor(() => m_database_ready);
        await m_database_lock.WaitAsync();
    }

    public static void ReleaseLock()
    {
        m_database_lock.Release();
    }

    public static void Initialize()
    {
        Load(XmlDatabase.Settings);
        Load(XmlDatabase.Favorites);
        Load(XmlDatabase.History);
        m_database_ready = true;
    }

    private static void Load(XmlData obj)
    {
        string filePath = Path.Combine(DatabaseFolderPath, obj.FileName);
        if (!File.Exists(filePath))
        {
            return;
        }

        var serializer = new XmlSerializer(obj.GetType());
        serializer.UnknownAttribute += (x, y) => Log("UnknownAttribute: " + y.ToString());
        serializer.UnknownElement += (x, y) => Log("UnknownElement: " + y.ToString());
        serializer.UnknownNode += (x, y) => Log("UnknownNode: " + y.ToString());
        serializer.UnreferencedObject += (x, y) => Log("UnreferencedObject: " + y.ToString());

        try
        {
            using Stream stream = File.OpenRead(filePath);
            obj.Target = serializer.Deserialize(stream) as XmlData;
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("5C89EA796A7DFC0A", e);
            return;
        }

        obj.Target.Unpack();
    }

    public static Action SaveSealed(XmlDatabaseItem item) =>
        () => SaveUnsealed(item).Wait();

    public static async Task<TaskException> SaveUnsealed(XmlDatabaseItem item)
    {
        Logger.I(TAG, "Saving: " + item.ToString());
        switch (item)
        {
            case XmlDatabaseItem.Favorites:
                await Save(XmlDatabase.Favorites);
                break;
            case XmlDatabaseItem.History:
                await Save(XmlDatabase.History);
                break;
            case XmlDatabaseItem.Settings:
                await Save(XmlDatabase.Settings);
                break;
            default:
                Logger.F(TAG, "SaveXmlDatabaseUnknownItem");
                return TaskException.InvalidParameters;
        }

        return TaskException.Success;
    }

    private static async Task Save(XmlData obj)
    {
        StorageFolder folder = await Storage.TryGetFolder(DatabaseFolderPath);
        if (folder == null)
        {
            Logger.AssertNotReachHere("1379ACEA3277A4C8");
            return;
        }
        await WaitLock();
        StorageFile file = await folder.CreateFileAsync(
            obj.FileName, CreationCollisionOption.ReplaceExisting);
        IRandomAccessStream stream = await file.OpenAsync(
            FileAccessMode.ReadWrite);

        obj.Pack();
        var serializer = new XmlSerializer(obj.GetType());
        serializer.Serialize(stream.AsStream(), obj);

        stream.Dispose();
        ReleaseLock();
    }

    private static void Log(string message)
    {
        Logger.I(TAG, message);
    }
}
