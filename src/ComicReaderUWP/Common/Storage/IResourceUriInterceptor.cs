// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.Common.Storage;

internal interface IResourceUriInterceptor
{
    bool TryParse(Uri uri, [NotNullWhen(true)] out IResourceUriHandler? handler);
}
