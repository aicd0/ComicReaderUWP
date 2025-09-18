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
        var scrollableContent = new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = options.Content,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = options.ContentSelectable,
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
        };

        var dialog = new ContentDialog
        {
            Title = options.Title,
            Content = scrollableContent,
            PrimaryButtonText = options.PrimaryButtonText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        if (!string.IsNullOrEmpty(options.SecondaryButtonText))
        {
            dialog.SecondaryButtonText = options.SecondaryButtonText;
        }

        if (options.PrimaryButtonClick != null)
        {
            dialog.PrimaryButtonClick += (s, e) =>
            {
                options.PrimaryButtonClick.Invoke(e);
            };
        }

        if (options.SecondaryButtonClick != null)
        {
            dialog.SecondaryButtonClick += (s, e) =>
            {
                options.SecondaryButtonClick.Invoke(e);
            };
        }

        return await dialog.ShowAsync();
    }

    public class DialogOptions
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public string PrimaryButtonText { get; private set; } = StringResourceProvider.Instance.OK;
        public string? SecondaryButtonText { get; private set; }
        public bool ContentSelectable { get; private set; } = false;

        public Action<ContentDialogButtonClickEventArgs>? PrimaryButtonClick { get; private set; }
        public Action<ContentDialogButtonClickEventArgs>? SecondaryButtonClick { get; private set; }

        private DialogOptions() { }

        public class Builder
        {
            private readonly DialogOptions _options = new();

            public Builder SetTitle(string title)
            {
                _options.Title = title;
                return this;
            }

            public Builder SetContent(string content, bool selectable = false)
            {
                _options.Content = content;
                _options.ContentSelectable = selectable;
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

            public Builder OnPrimaryButtonClick(Action<ContentDialogButtonClickEventArgs> handler)
            {
                _options.PrimaryButtonClick = handler;
                return this;
            }

            public Builder OnSecondaryButtonClick(Action<ContentDialogButtonClickEventArgs> handler)
            {
                _options.SecondaryButtonClick = handler;
                return this;
            }

            public DialogOptions Build()
            {
                return _options;
            }
        }
    }
}
