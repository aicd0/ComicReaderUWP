// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.DataModels;

public class DialogOptions
{
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string PrimaryButtonText { get; private set; } = string.Empty;
    public string? SecondaryButtonText { get; private set; }
    public string? CloseButtonText { get; private set; }
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

        public Builder SetCloseButtonText(string? text)
        {
            _options.CloseButtonText = text;
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
