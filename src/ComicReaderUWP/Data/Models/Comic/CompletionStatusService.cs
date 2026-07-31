// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Localization;

namespace ComicReaderUWP.Data.Models.Comic;

internal static class CompletionStatusService
{
    public static readonly IReadOnlyList<CompletionStatusEnum> AllStatus = [
        CompletionStatusEnum.Abandoned,
        CompletionStatusEnum.OnHold,
        CompletionStatusEnum.Unread,
        CompletionStatusEnum.PlanToRead,
        CompletionStatusEnum.Reading,
        CompletionStatusEnum.Completed,
    ];

    public static int EnumToComparable(CompletionStatusEnum state)
    {
        return state switch
        {
            CompletionStatusEnum.Abandoned => 1,
            CompletionStatusEnum.OnHold => 2,
            CompletionStatusEnum.Unread => 3,
            CompletionStatusEnum.PlanToRead => 4,
            CompletionStatusEnum.Reading => 5,
            CompletionStatusEnum.Completed => 6,
            _ => 0,
        };
    }

    public static string EnumToString(CompletionStatusEnum status, string? fallback = null)
    {
        return status switch
        {
            CompletionStatusEnum.Unread => StringResourceProvider.Instance.CompletionStatusUnread,
            CompletionStatusEnum.Reading => StringResourceProvider.Instance.CompletionStatusReading,
            CompletionStatusEnum.Completed => StringResourceProvider.Instance.CompletionStatusCompleted,
            CompletionStatusEnum.Abandoned => StringResourceProvider.Instance.CompletionStatusAbandoned,
            CompletionStatusEnum.PlanToRead => StringResourceProvider.Instance.CompletionStatusPlanToRead,
            CompletionStatusEnum.OnHold => StringResourceProvider.Instance.CompletionStatusOnHold,
            _ => fallback is null ? throw new ArgumentException($"Unknwon completion status '{status}'.") : fallback,
        };
    }

    public static SDK.Plugins.Comic.CompletionStatusEnum HostEnumToSDKEnum(CompletionStatusEnum status)
    {
        return status switch
        {
            CompletionStatusEnum.Unread => SDK.Plugins.Comic.CompletionStatusEnum.Unread,
            CompletionStatusEnum.Reading => SDK.Plugins.Comic.CompletionStatusEnum.Reading,
            CompletionStatusEnum.Completed => SDK.Plugins.Comic.CompletionStatusEnum.Completed,
            CompletionStatusEnum.Abandoned => SDK.Plugins.Comic.CompletionStatusEnum.Abandoned,
            CompletionStatusEnum.PlanToRead => SDK.Plugins.Comic.CompletionStatusEnum.PlanToRead,
            CompletionStatusEnum.OnHold => SDK.Plugins.Comic.CompletionStatusEnum.OnHold,
            _ => throw new ArgumentException($"Unknwon completion status '{status}'."),
        };
    }

    public static CompletionStatusEnum SDKEnumToHostEnum(SDK.Plugins.Comic.CompletionStatusEnum status)
    {
        return status switch
        {
            SDK.Plugins.Comic.CompletionStatusEnum.Unread => CompletionStatusEnum.Unread,
            SDK.Plugins.Comic.CompletionStatusEnum.Reading => CompletionStatusEnum.Reading,
            SDK.Plugins.Comic.CompletionStatusEnum.Completed => CompletionStatusEnum.Completed,
            SDK.Plugins.Comic.CompletionStatusEnum.Abandoned => CompletionStatusEnum.Abandoned,
            SDK.Plugins.Comic.CompletionStatusEnum.PlanToRead => CompletionStatusEnum.PlanToRead,
            SDK.Plugins.Comic.CompletionStatusEnum.OnHold => CompletionStatusEnum.OnHold,
            _ => throw new ArgumentException($"Unknwon completion status '{status}'."),
        };
    }

    public static bool CanTransitToReadingAutomatically(CompletionStatusEnum status)
    {
        return status switch
        {
            CompletionStatusEnum.Unread => true,
            CompletionStatusEnum.Reading => false,
            CompletionStatusEnum.Completed => false,
            CompletionStatusEnum.Abandoned => false,
            CompletionStatusEnum.PlanToRead => true,
            CompletionStatusEnum.OnHold => false,
            _ => false,
        };
    }
}
