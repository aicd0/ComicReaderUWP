// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Database.SqlHelpers;

namespace ComicReaderUWP.Common.Expression.Search.Sql;

internal interface ISQLCommandProvider
{
    bool IsHiddenFilter(string key);

    ICondition CreateNotHiddenCondition();

    ICondition CreateFilterCondition(string key, string value);
}
