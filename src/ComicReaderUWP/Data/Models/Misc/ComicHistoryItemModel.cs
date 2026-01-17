// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.SDK.Database.SqlHelpers;

namespace ComicReaderUWP.Data.Models.Misc;

internal class ComicHistoryItemModel
{
    //
    // Properties
    //

    public long ComicId { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset DateTime { get; protected set; }

    private ComicHistoryItemModel(long comicId, string title, DateTimeOffset dateTime)
    {
        ComicId = comicId;
        Title = title;
        DateTime = dateTime;
    }

    //
    // Getters
    //

    public static async Task<List<ComicHistoryItemModel>> GetAllAsync()
    {
        return await Enqueue("GetAllAsync", () =>
        {
            var command = SelectCommand.Create(ComicHistoryTable.Instance);
            IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicHistoryTable.ColumnComicId);
            IReaderToken<string> titleToken = command.PutQueryString(ComicHistoryTable.ColumnTitle);
            IReaderToken<long> timeToken = command.PutQueryInt64(ComicHistoryTable.ColumnTime);
            using SelectCommand.IReader reader = command.Execute();
            List<ComicHistoryItemModel> items = [];
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                string title = titleToken.GetValue();
                long time = timeToken.GetValue();
                ComicHistoryItemModel item = new(comicId, title, DateTimeOffset.FromUnixTimeMilliseconds(time));
                items.Add(item);
            }

            return items;
        });
    }

    public static async Task<bool> IsEmptyAsync()
    {
        return await Enqueue("IsEmptyAsync", () =>
        {
            SelectCommand command = SelectCommand.Create(ComicHistoryTable.Instance)
                .Limit(1);
            command.PutQueryInt64(ComicHistoryTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            return !reader.Read();
        });
    }

    //
    // Setters
    //

    public static async Task AddAsync(long id, string title, bool suppressEvent = false)
    {
        await Enqueue("AddAsync", () =>
        {
            DeleteCommand deleteCommand = DeleteCommand.Create(ComicHistoryTable.Instance)
                .AppendCondition(ComicHistoryTable.ColumnComicId, id);
            deleteCommand.Execute();

            InsertCommand insertCommand = InsertCommand.Create(ComicHistoryTable.Instance)
                .AppendColumn(ComicHistoryTable.ColumnComicId, id)
                .AppendColumn(ComicHistoryTable.ColumnTitle, title)
                .AppendColumn(ComicHistoryTable.ColumnTime, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            insertCommand.Execute();

            return true;
        });

        if (!suppressEvent)
        {
            DispatchUpdateEvent();
        }
    }

    public static async Task RemoveAsync(long id, bool suppressEvent = false)
    {
        bool changed = await Enqueue("RemoveAsync", () =>
        {
            DeleteCommand deleteCommand = DeleteCommand.Create(ComicHistoryTable.Instance)
                .AppendCondition(ComicHistoryTable.ColumnComicId, id);
            int rowsAffected = deleteCommand.Execute();
            return rowsAffected > 0;
        });

        if (changed && !suppressEvent)
        {
            DispatchUpdateEvent();
        }
    }

    public static async Task ClearAsync(bool suppressEvent = false)
    {
        await Enqueue("ClearAsync", () =>
        {
            var deleteCommand = DeleteCommand.Create(ComicHistoryTable.Instance);
            deleteCommand.Execute();
            return true;
        });

        if (!suppressEvent)
        {
            DispatchUpdateEvent();
        }
    }

    private static void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.HistoryUpdated.Emit(0);
    }

    private static async Task<T> Enqueue<T>(string taskName, Func<T> op)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        SqliteDB.MiscDatabaseDispatcher.Submit(taskName, delegate
        {
            taskResult.SetResult(op());
        });

        return await taskResult.Task;
    }
}
