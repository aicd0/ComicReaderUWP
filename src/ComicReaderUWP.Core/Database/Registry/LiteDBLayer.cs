// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.SDK.Models;

using LiteDB;

namespace ComicReaderUWP.Core.Database.Registry;

internal partial class LiteDBLayer(string databasePath) : IRegistryDatabase
{
    private const string TAG = nameof(LiteDBLayer);
    private const int VERSION = 2;
    private const string KEY_COLLECTION_PREFIX = "key_";
    private const string META_KEY_VERSION = "Version";

    private readonly object _initLock = new();
    private readonly string _databasePath = databasePath;
    private readonly ConcurrentDictionary<string, RegistryKey?> _keyCache = [];
    private LiteDatabase? _db;
    private ILiteCollection<RegistryKeyDocument>? _keysCollection;

    public void Dispose()
    {
        _db?.Dispose();
        _db = null;
        _keysCollection = null;
        _keyCache.Clear();
    }

    public IEnumerable<string> GetKeys(string path, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));
        path = NormalizePath(path);

        if (!recursive)
        {
            foreach (RegistryKeyDocument key in GetKeysCollection().Find(x => x.ParentPath == path))
            {
                yield return key.Path;
            }

            yield break;
        }

        List<string> parentPaths = [path];
        List<string> nextParentPaths = [];
        while (parentPaths.Count > 0)
        {
            foreach (string parentPath in parentPaths)
            {
                foreach (RegistryKeyDocument key in GetKeysCollection().Find(x => x.ParentPath == parentPath))
                {
                    nextParentPaths.Add(key.Path);
                    yield return key.Path;
                }
            }

            (nextParentPaths, parentPaths) = (parentPaths, nextParentPaths);
            nextParentPaths.Clear();
        }
    }

    public IRegistryKey CreateKey(string path)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));
        path = NormalizePath(path);

        if (_keyCache.TryGetValue(path, out RegistryKey? key) && key is not null)
        {
            return key;
        }

        ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection();
        lock (_keyCache)
        {
            return CreateKeyNoLock(keysCollection, path);
        }
    }

    public bool TryGetKey(string path, [NotNullWhen(true)] out IRegistryKey? key)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));
        path = NormalizePath(path);

        if (_keyCache.TryGetValue(path, out RegistryKey? cachedKey))
        {
            key = cachedKey;
            return key is not null;
        }

        ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection();
        lock (_keyCache)
        {
            bool result = TryGetKeyNoLock(keysCollection, path, out RegistryKey? internalKey);
            key = internalKey;
            return result;
        }
    }

    public bool RemoveKey(string path)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));
        path = NormalizePath(path);

        ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection();
        RegistryKeyDocument? keyDocument = keysCollection.FindOne(x => x.Path == path);
        if (keyDocument is null)
        {
            return false;
        }

        lock (_keyCache)
        {
            keyDocument = keysCollection.FindOne(x => x.Path == path);
            if (keyDocument is null)
            {
                return false;
            }

            List<RegistryKeyDocument> parentDocs = [keyDocument];
            List<RegistryKeyDocument> nextParentDocs = [];
            while (parentDocs.Count > 0)
            {
                foreach (RegistryKeyDocument parentDoc in parentDocs)
                {
                    keysCollection.Delete(parentDoc.Id);
                    _keyCache[parentDoc.Path] = null;
                    foreach (RegistryKeyDocument key in GetKeysCollection().Find(x => x.ParentPath == parentDoc.Path))
                    {
                        nextParentDocs.Add(key);
                    }
                }

                (nextParentDocs, parentDocs) = (parentDocs, nextParentDocs);
                nextParentDocs.Clear();
            }

            return true;
        }
    }

    public void CopyTree(string srcPath, string dstPath)
    {
        ArgumentNullException.ThrowIfNull(srcPath, nameof(srcPath));
        srcPath = NormalizePath(srcPath);
        ArgumentNullException.ThrowIfNull(dstPath, nameof(dstPath));
        dstPath = NormalizePath(dstPath);

        ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection();

        lock (_keyCache)
        {
            RegistryKeyDocument? srcDoc = keysCollection.FindOne(x => x.Path == srcPath) ??
                throw new InvalidOperationException($"Registry key '{srcPath}' not found.");

            RegistryKeyDocument? existingDst = keysCollection.FindOne(x => x.Path == dstPath);
            if (existingDst is not null)
            {
                throw new InvalidOperationException($"Registry key '{dstPath}' already exists.");
            }

            List<RegistryKeyDocument> toCopy = [];

            List<RegistryKeyDocument> parentDocs = [srcDoc];
            List<RegistryKeyDocument> nextParentDocs = [];
            while (parentDocs.Count > 0)
            {
                foreach (RegistryKeyDocument parentDoc in parentDocs)
                {
                    toCopy.Add(parentDoc);
                    foreach (RegistryKeyDocument child in keysCollection.Find(x => x.ParentPath == parentDoc.Path))
                    {
                        nextParentDocs.Add(child);
                    }
                }

                (parentDocs, nextParentDocs) = (nextParentDocs, parentDocs);
                nextParentDocs.Clear();
            }

            foreach (RegistryKeyDocument doc in toCopy)
            {
                if (!TryGetKeyNoLock(keysCollection, doc.Path, out RegistryKey? srcKey))
                {
                    throw new InvalidOperationException($"Registry key '{doc.Path}' not found.");
                }

                string newPath = dstPath + doc.Path[srcPath.Length..];
                RegistryKey dstKey = CreateKeyNoLock(keysCollection, newPath);

                srcKey.CopyTo(dstKey);
            }
        }
    }

    private bool TryGetKeyNoLock(ILiteCollection<RegistryKeyDocument> keysCollection, string path, [NotNullWhen(true)] out RegistryKey? key)
    {
        if (_keyCache.TryGetValue(path, out RegistryKey? cachedKey))
        {
            key = cachedKey;
            return key is not null;
        }

        RegistryKeyDocument? keyDocument = keysCollection.FindOne(x => x.Path == path);
        if (keyDocument is null)
        {
            _keyCache[path] = null;
            key = null;
            return false;
        }

        cachedKey = new(this, keyDocument.CollectionName);
        _keyCache[path] = cachedKey;
        key = cachedKey;
        return true;
    }

    private RegistryKey CreateKeyNoLock(ILiteCollection<RegistryKeyDocument> keysCollection, string path)
    {
        if (_keyCache.TryGetValue(path, out RegistryKey? key) && key is not null)
        {
            return key;
        }

        RegistryKeyDocument? keyDocument = keysCollection.FindOne(x => x.Path == path);
        if (keyDocument is null)
        {
            string parentPath = GetParentPath(path);

            EnsurePath(keysCollection, parentPath);

            keyDocument = new()
            {
                Path = path,
                ParentPath = parentPath,
                CollectionName = GenerateCollectionName(),
            };
            keysCollection.Insert(keyDocument);
        }

        key = new(this, keyDocument.CollectionName);
        _keyCache[path] = key;
        return key;
    }

    private ILiteCollection<RegistryKeyDocument> GetKeysCollection()
    {
        ILiteCollection<RegistryKeyDocument>? collection = _keysCollection;
        if (collection is not null)
        {
            return collection;
        }

        lock (_initLock)
        {
            collection = _keysCollection;
            if (collection is not null)
            {
                return collection;
            }

            collection = GetKeysCollection(GetDatabase());
            _keysCollection = collection;
            return collection;
        }
    }

    private LiteDatabase GetDatabase()
    {
        LiteDatabase? db = _db;
        if (db is not null)
        {
            return db;
        }

        lock (_initLock)
        {
            db = _db;
            if (db is not null)
            {
                return db;
            }

            string? databaseFolder = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(databaseFolder))
            {
                Directory.CreateDirectory(databaseFolder);
            }

            db = new LiteDatabase($"Filename={_databasePath}; Mode=Shared;");
            InitializeDatabase(db);
            _db = db;
            return db;
        }
    }

    private void InitializeDatabase(LiteDatabase db)
    {
        db.Mapper.EmptyStringToNull = false;
        db.Mapper.TrimWhitespace = false;

        UpgradeDatabase(db);

        ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection(db);

        HashSet<string> keys = [];
        HashSet<string> parentPaths = [];
        HashSet<string> referencedCollections = [];
        foreach (RegistryKeyDocument keyDocument in keysCollection.FindAll())
        {
            keys.Add(keyDocument.Path);
            parentPaths.Add(keyDocument.ParentPath);
            referencedCollections.Add(keyDocument.CollectionName);
        }

        parentPaths.Remove(string.Empty);
        foreach (string parentPath in parentPaths)
        {
            if (!keys.Contains(parentPath))
            {
                Logger.F(TAG, $"Parent path '{parentPath}' not found");
                EnsurePath(keysCollection, parentPath);
            }
        }

        List<string> unreferencedCollections = [];
        foreach (string name in db.GetCollectionNames())
        {
            if (!name.StartsWith(KEY_COLLECTION_PREFIX))
            {
                continue;
            }

            string realName = name[KEY_COLLECTION_PREFIX.Length..];
            if (!referencedCollections.Contains(realName))
            {
                unreferencedCollections.Add(name);
            }
        }

        foreach (string name in unreferencedCollections)
        {
            db.DropCollection(name);
        }

        // (Optional) Publish for fast reuse
        _keysCollection = keysCollection;
    }

    private static void UpgradeDatabase(LiteDatabase db)
    {
        bool isNewDatabase = !db.GetCollectionNames().Contains("keys");

        ILiteCollection<MetaDocument> metaCollection = GetMetaCollection(db);
        MetaDocument? versionDoc = metaCollection.FindById(META_KEY_VERSION);

        int version;
        if (versionDoc is null)
        {
            version = isNewDatabase ? VERSION : 1;
        }
        else
        {
            version = versionDoc.Value.AsInt32;
        }

        if (version > VERSION)
        {
            throw new InvalidOperationException($"The database version ({version}) is larger than the target version ({VERSION}).");
        }

        switch (version)
        {
            case 1:
                {
                    ILiteCollection<RegistryKeyDocument> keysCollection = GetKeysCollection(db);
                    List<RegistryKeyDocument> allDocs = [.. keysCollection.FindAll()];
                    foreach (RegistryKeyDocument doc in allDocs)
                    {
                        doc.Path = doc.Path.TrimEnd('/');
                        doc.ParentPath = doc.ParentPath.TrimEnd('/');
                        keysCollection.Upsert(doc);
                    }
                }

                goto case VERSION;
            case VERSION:
                break;
            default:
                throw new InvalidOperationException($"Invalid database version ({version}).");
        }

        if (versionDoc is null || version != VERSION)
        {
            metaCollection.Upsert(new MetaDocument()
            {
                Key = META_KEY_VERSION,
                Value = VERSION,
            });
        }
    }

    private static ILiteCollection<MetaDocument> GetMetaCollection(LiteDatabase db)
    {
        ILiteCollection<MetaDocument>? collection = db.GetCollection<MetaDocument>("meta");
        collection.EnsureIndex(x => x.Key, unique: true);
        return collection;
    }

    private static ILiteCollection<RegistryKeyDocument> GetKeysCollection(LiteDatabase db)
    {
        ILiteCollection<RegistryKeyDocument>? collection = db.GetCollection<RegistryKeyDocument>("keys");
        collection.EnsureIndex(x => x.Path, unique: true);
        collection.EnsureIndex(x => x.ParentPath);
        return collection;
    }

    private static void EnsurePath(ILiteCollection<RegistryKeyDocument> keysCollection, string path)
    {
        while (path != string.Empty)
        {
            RegistryKeyDocument? key = keysCollection.FindOne(x => x.Path == path);
            if (key is not null)
            {
                return;
            }

            string parentPath = GetParentPath(path);
            keysCollection.Insert(new RegistryKeyDocument()
            {
                Path = path,
                ParentPath = parentPath,
                CollectionName = GenerateCollectionName(),
            });

            path = parentPath;
        }
    }

    private static string NormalizePath(string path)
    {
        if (!PathRegex().IsMatch(path))
        {
            throw new ArgumentException($"Invalid path '{path}'.");
        }

        return path;
    }

    private static string GetParentPath(string path)
    {
        int index = path.LastIndexOf('/');
        if (index < 0)
        {
            throw new ArgumentException(null, nameof(path));
        }

        return path[..index];
    }

    private static string GenerateCollectionName()
    {
        const int length = 16;
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new StringBuilder(length);
        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        for (int i = 0; i < length; i++)
        {
            result.Append(chars[buffer[i] % chars.Length]);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"^(/[A-Za-z0-9_\-]+)+$")]
    private static partial Regex PathRegex();

    private class RegistryKey(LiteDBLayer layer, string name) : IRegistryKey
    {
        private readonly Lazy<ILiteCollection<RegistryEntryDocument>> _collection = new(() =>
        {
            LiteDatabase db = layer.GetDatabase();
            return db.GetCollection<RegistryEntryDocument>($"{KEY_COLLECTION_PREFIX}{name}");
        });

        public int Count => _collection.Value.Count();

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (RegistryEntryDocument entry in _collection.Value.FindAll())
                {
                    yield return entry.Key;
                }
            }
        }

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            if (TryGetNormal(key, out T? internalValue))
            {
                value = internalValue;
                return true;
            }

            value = default;
            return false;
        }

        private bool TryGetNormal<T>(string key, [NotNullWhen(true)] out T? value)
        {
            ILiteCollection<RegistryEntryDocument> collection = _collection.Value;
            RegistryEntryDocument? entry = collection.FindById(key);
            if (entry is null || entry.Value.IsNull)
            {
                value = default;
                return false;
            }

            bool typeMatched = entry.Value.Type switch
            {
                BsonType.Int32 => typeof(T) == typeof(int) || typeof(T) == typeof(long),
                BsonType.Int64 => typeof(T) == typeof(long),
                BsonType.Double => typeof(T) == typeof(float) || typeof(T) == typeof(double),
                BsonType.Decimal => typeof(T) == typeof(decimal),
                BsonType.Binary => typeof(T) == typeof(byte[]),
                BsonType.String => typeof(T) == typeof(string),
                BsonType.Guid => typeof(T) == typeof(Guid),
                BsonType.Boolean => typeof(T) == typeof(bool),
                BsonType.DateTime => typeof(T) == typeof(DateTime),
                _ => false,
            };
            if (typeMatched)
            {
                value = (T)entry.Value.RawValue;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            ILiteCollection<RegistryEntryDocument> collection = _collection.Value;
            BsonValue bsonValue = value switch
            {
                int typedValue => new BsonValue(typedValue),
                long typedValue => new BsonValue(typedValue),
                float typedValue => new BsonValue((double)typedValue),
                double typedValue => new BsonValue(typedValue),
                decimal typedValue => new BsonValue(typedValue),
                byte[] typedValue => new BsonValue(typedValue),
                string typedValue => new BsonValue(typedValue),
                Guid typedValue => new BsonValue(typedValue),
                bool typedValue => new BsonValue(typedValue),
                DateTime typedValue => new BsonValue(typedValue),
                _ => throw new NotSupportedException($"Type {typeof(T)} is not supported."),
            };
            collection.Upsert(key, new RegistryEntryDocument
            {
                Key = key,
                Value = bsonValue,
            });
        }

        public bool Remove(string key)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            ILiteCollection<RegistryEntryDocument> collection = _collection.Value;
            return collection.Delete(key);
        }

        public void CopyTo(RegistryKey dstKey)
        {
            ILiteCollection<RegistryEntryDocument> srcCollection = _collection.Value;
            ILiteCollection<RegistryEntryDocument> dstCollection = dstKey._collection.Value;

            foreach (RegistryEntryDocument doc in srcCollection.FindAll())
            {
                dstCollection.Upsert(doc);
            }
        }
    }

    private class MetaDocument
    {
        [BsonId]
        public required string Key { get; set; }

        public required BsonValue Value { get; set; }
    }

    private class RegistryKeyDocument
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.Empty;

        public required string Path { get; set; }

        public required string ParentPath { get; set; }

        public required string CollectionName { get; set; }
    }

    private class RegistryEntryDocument
    {
        [BsonId]
        public required string Key { get; set; }

        public required BsonValue Value { get; set; }
    }
}
