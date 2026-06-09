// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.ServiceManagement.Models;

namespace ComicReaderUWP.Core.Common.ServiceManagement.Services;

public interface INativeService : IService
{
    NativeDialogResult ShowYesNoDialog(string caption, string text);
}
