// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.IO;
using System.Xml.Serialization;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

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

internal class XmlDatabaseManager
{
    private const string TAG = "XmlDatabaseManager";

    private static string DatabaseFolderPath => StorageLocation.LocalFolderPath;

    public static void Initialize()
    {
        Load(XmlDatabase.Settings);
        Load(XmlDatabase.Favorites);
        Load(XmlDatabase.History);
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

    private static void Log(string message)
    {
        Logger.I(TAG, message);
    }
}
