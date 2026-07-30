// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Localization;

namespace ComicReaderUWP.Data.Models.Comic;

internal static class ComicCompletionStatusService
{
    public static readonly IReadOnlyList<ComicCompletionStatusEnum> AllStatus = [
        ComicCompletionStatusEnum.Abandoned,
        ComicCompletionStatusEnum.OnHold,
        ComicCompletionStatusEnum.Unread,
        ComicCompletionStatusEnum.PlanToRead,
        ComicCompletionStatusEnum.Reading,
        ComicCompletionStatusEnum.Completed,
    ];

    public static int EnumToComparable(ComicCompletionStatusEnum state)
    {
        return state switch
        {
            ComicCompletionStatusEnum.Abandoned => 1,
            ComicCompletionStatusEnum.OnHold => 2,
            ComicCompletionStatusEnum.Unread => 3,
            ComicCompletionStatusEnum.PlanToRead => 4,
            ComicCompletionStatusEnum.Reading => 5,
            ComicCompletionStatusEnum.Completed => 6,
            _ => 0,
        };
    }

    public static string EnumToString(ComicCompletionStatusEnum status, string? fallback = null)
    {
        return status switch
        {
            ComicCompletionStatusEnum.Unread => StringResourceProvider.Instance.CompletionStatusUnread,
            ComicCompletionStatusEnum.Reading => StringResourceProvider.Instance.CompletionStatusReading,
            ComicCompletionStatusEnum.Completed => StringResourceProvider.Instance.CompletionStatusCompleted,
            ComicCompletionStatusEnum.Abandoned => StringResourceProvider.Instance.CompletionStatusAbandoned,
            ComicCompletionStatusEnum.PlanToRead => StringResourceProvider.Instance.CompletionStatusPlanToRead,
            ComicCompletionStatusEnum.OnHold => StringResourceProvider.Instance.CompletionStatusOnHold,
            _ => fallback is null ? throw new ArgumentException($"Unknwon completion status '{status}'.") : fallback,
        };
    }

    public static SDK.Plugins.Comic.CompletionStatusEnum HostEnumToSDKEnum(ComicCompletionStatusEnum status)
    {
        return status switch
        {
            ComicCompletionStatusEnum.Unread => SDK.Plugins.Comic.CompletionStatusEnum.Unread,
            ComicCompletionStatusEnum.Reading => SDK.Plugins.Comic.CompletionStatusEnum.Reading,
            ComicCompletionStatusEnum.Completed => SDK.Plugins.Comic.CompletionStatusEnum.Completed,
            ComicCompletionStatusEnum.Abandoned => SDK.Plugins.Comic.CompletionStatusEnum.Abandoned,
            ComicCompletionStatusEnum.PlanToRead => SDK.Plugins.Comic.CompletionStatusEnum.PlanToRead,
            ComicCompletionStatusEnum.OnHold => SDK.Plugins.Comic.CompletionStatusEnum.OnHold,
            _ => throw new ArgumentException($"Unknwon completion status '{status}'."),
        };
    }

    public static ComicCompletionStatusEnum SDKEnumToHostEnum(SDK.Plugins.Comic.CompletionStatusEnum status)
    {
        return status switch
        {
            SDK.Plugins.Comic.CompletionStatusEnum.Unread => ComicCompletionStatusEnum.Unread,
            SDK.Plugins.Comic.CompletionStatusEnum.Reading => ComicCompletionStatusEnum.Reading,
            SDK.Plugins.Comic.CompletionStatusEnum.Completed => ComicCompletionStatusEnum.Completed,
            SDK.Plugins.Comic.CompletionStatusEnum.Abandoned => ComicCompletionStatusEnum.Abandoned,
            SDK.Plugins.Comic.CompletionStatusEnum.PlanToRead => ComicCompletionStatusEnum.PlanToRead,
            SDK.Plugins.Comic.CompletionStatusEnum.OnHold => ComicCompletionStatusEnum.OnHold,
            _ => throw new ArgumentException($"Unknwon completion status '{status}'."),
        };
    }

    public static bool CanTransitToReadingAutomatically(ComicCompletionStatusEnum status)
    {
        return status switch
        {
            ComicCompletionStatusEnum.Unread => true,
            ComicCompletionStatusEnum.Reading => false,
            ComicCompletionStatusEnum.Completed => false,
            ComicCompletionStatusEnum.Abandoned => false,
            ComicCompletionStatusEnum.PlanToRead => true,
            ComicCompletionStatusEnum.OnHold => false,
            _ => false,
        };
    }
}
