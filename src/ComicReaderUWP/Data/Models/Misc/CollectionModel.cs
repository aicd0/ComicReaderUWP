// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Data.Models.Misc;

internal static class CollectionModel
{
    private const string TAG = nameof(CollectionModel);

    //
    // Queries
    //

    public static async Task<IReadOnlyList<long>> GetAllCollectionIds()
    {
        return await ComicHandle.Enqueue(() =>
        {
            SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                .AppendCondition(ComicTable.ColumnType, (long)ComicType.Collection);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            List<long> results = [];
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                results.Add(idToken.GetValue());
            }
            return results;
        });
    }

    public static async Task<IReadOnlyList<long>> GetCollectionIds(long comicId)
    {
        if (comicId < 0)
        {
            return [];
        }

        return await ComicHandle.Enqueue(() =>
        {
            SelectCommand command = SelectCommand.Create(ComicCollectionTable.Instance)
                .AppendCondition(ComicCollectionTable.ColumnComicId, comicId);
            IReaderToken<long> collectionIdToken = command.PutQueryInt64(ComicCollectionTable.ColumnCollectionId);
            List<long> ids = [];
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                ids.Add(collectionIdToken.GetValue());
            }
            return ids;
        });
    }

    public static async Task<IReadOnlyList<long>> GetComicIds(ComicModel collection)
    {
        if (collection.IsExternal)
        {
            return [];
        }

        long collectionId = collection.Id;
        return await ComicHandle.Enqueue(() => QueryComicIdsNoLock(collectionId));
    }

    //
    // Writes
    //

    public static async Task AddComics(ComicModel collection, IEnumerable<long> comicIds)
    {
        long collectionId = collection.Id;
        List<long> ids = [.. comicIds.Where(x => x >= 0 && x != collectionId).Distinct()];
        if (ids.Count == 0)
        {
            return;
        }

        int insertedCount = await ComicHandle.Enqueue(() =>
        {
            if (collection.IsExternal)
            {
                Logger.F(TAG, "AddComics: Cannot add comics to a collection which is not saved");
                return 0;
            }

            int inserted = 0;
            SqliteDB.MainDatabase.WithTransaction(() =>
            {
                HashSet<long> existingIds = QueryLinkedComicIdsNoLock(collectionId, ids);
                List<long> newIds = [.. ids.Where(x => !existingIds.Contains(x))];
                foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(newIds))
                {
                    foreach (long comicId in idChunk)
                    {
                        InsertCommand.Create(ComicCollectionTable.Instance)
                            .AppendColumn(ComicCollectionTable.ColumnCollectionId, collectionId)
                            .AppendColumn(ComicCollectionTable.ColumnComicId, comicId)
                            .Execute();
                        inserted++;
                    }
                }
            });
            return inserted;
        });

        if (insertedCount > 0)
        {
            DispatchUpdateEvent();
        }
    }

    public static async Task RemoveComics(ComicModel collection, IEnumerable<long> comicIds)
    {
        long collectionId = collection.Id;
        List<long> ids = [.. comicIds.Where(x => x >= 0).Distinct()];
        if (ids.Count == 0)
        {
            return;
        }

        int removedCount = await ComicHandle.Enqueue(() =>
        {
            if (collection.IsExternal)
            {
                Logger.F(TAG, "RemoveComics: Cannot remove comics from a collection which is not saved");
                return 0;
            }

            int removed = 0;
            SqliteDB.MainDatabase.WithTransaction(() =>
            {
                foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(ids))
                {
                    removed += DeleteCommand.Create(ComicCollectionTable.Instance)
                        .AppendCondition(ComicCollectionTable.ColumnCollectionId, collectionId)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicCollectionTable.ColumnComicId), idChunk))
                        .Execute();
                }
            });
            return removed;
        });

        if (removedCount > 0)
        {
            DispatchUpdateEvent();
        }
    }

    //
    // Private helpers
    //

    private static List<long> QueryComicIdsNoLock(long collectionId)
    {
        SelectCommand command = SelectCommand.Create(ComicCollectionTable.Instance)
            .AppendCondition(ComicCollectionTable.ColumnCollectionId, collectionId);
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicCollectionTable.ColumnComicId);
        List<long> ids = [];
        using SelectCommand.IReader reader = command.Execute();
        while (reader.Read())
        {
            ids.Add(comicIdToken.GetValue());
        }
        return ids;
    }

    private static HashSet<long> QueryLinkedComicIdsNoLock(long collectionId, List<long> comicIds)
    {
        HashSet<long> ids = [];
        foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(comicIds))
        {
            SelectCommand command = SelectCommand.Create(ComicCollectionTable.Instance)
                .AppendCondition(ComicCollectionTable.ColumnCollectionId, collectionId)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicCollectionTable.ColumnComicId), idChunk));
            IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicCollectionTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                ids.Add(comicIdToken.GetValue());
            }
        }
        return ids;
    }

    private static void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.CollectionUpdated.Emit(0);
    }
}
