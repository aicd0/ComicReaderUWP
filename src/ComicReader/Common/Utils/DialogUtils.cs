// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.Utils;

internal class DialogUtils
{
    public static async Task<ContentDialogResult> ShowDialogAsync(XamlRoot xamlRoot, DialogOptions options)
    {
        var dialog = new ContentDialog
        {
            Title = options.Title,
            Content = options.Content,
            PrimaryButtonText = options.PrimaryButtonText,
            XamlRoot = xamlRoot
        };

        if (!string.IsNullOrEmpty(options.SecondaryButtonText))
        {
            dialog.SecondaryButtonText = options.SecondaryButtonText;
        }

        if (!string.IsNullOrEmpty(options.CloseButtonText))
        {
            dialog.CloseButtonText = options.CloseButtonText;
        }

        return await dialog.ShowAsync();
    }

    public class DialogOptions
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public string PrimaryButtonText { get; private set; } = string.Empty;
        public string? SecondaryButtonText { get; private set; }
        public string? CloseButtonText { get; private set; }

        private DialogOptions() { }

        public class Builder
        {
            private readonly DialogOptions _options = new();

            public Builder SetTitle(string title)
            {
                _options.Title = title;
                return this;
            }

            public Builder SetContent(string content)
            {
                _options.Content = content;
                return this;
            }

            public Builder SetPrimaryButtonText(string text)
            {
                _options.PrimaryButtonText = text;
                return this;
            }

            public Builder SetSecondaryButtonText(string? text)
            {
                _options.SecondaryButtonText = text;
                return this;
            }

            public Builder SetCloseButtonText(string? text)
            {
                _options.CloseButtonText = text;
                return this;
            }

            public DialogOptions Build()
            {
                return _options;
            }
        }
    }
}
