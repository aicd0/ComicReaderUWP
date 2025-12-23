// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Common.Expression.Search.Sql;

internal interface ISQLCommandProvider
{
    ICondition CreateFilterCondition(string key, string value);

    ICondition? GetAdditionalCondition();
}
