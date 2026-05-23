// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Models;

public class DialogResult
{
    public required bool Success { get; init; }
    public required ContentDialogResult Result { get; init; }

    public static DialogResult FromFailure()
    {
        return new DialogResult
        {
            Success = false,
            Result = ContentDialogResult.None,
        };
    }

    public static DialogResult FromSuccess(ContentDialogResult result)
    {
        return new DialogResult
        {
            Success = true,
            Result = result,
        };
    }
}
