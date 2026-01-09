// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Database.SqlHelpers;

internal interface ICommandContext
{
    string AppendParameter(object value);
}
