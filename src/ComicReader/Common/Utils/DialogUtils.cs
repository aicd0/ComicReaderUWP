// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReader.Common.Threading;
using ComicReader.SDK.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.Utils;

internal class DialogUtils
{
    private const string TAG = nameof(DialogUtils);

    private static readonly Dictionary<int, Queue<PendingDialogItem>> _windowDialogQueue = [];

    public static Task<ContentDialogResult> EnqueueDialogAsync(DialogOptions options)
    {
        TaskCompletionSource<ContentDialogResult> resultSource = new();
        MainThreadUtils.RunInMainThread(() =>
        {
            MainWindow? window = App.WindowManager.GetActiveWindow() ?? App.WindowManager.GetAnyWindow();
            if (window is null)
            {
                Logger.F(TAG, "ShowDialogAtActiveWindowAsync: Window not found.");
                resultSource.SetResult(ContentDialogResult.None);
                return;
            }

            ContentDialog dialog = CreateDialog(options);
            EnqueueDialogInternal(resultSource, window.WindowId, dialog);
        });

        return resultSource.Task;
    }

    public static Task<ContentDialogResult> EnqueueDialogAsync(int windowId, DialogOptions options)
    {
        TaskCompletionSource<ContentDialogResult> resultSource = new();
        MainThreadUtils.RunInMainThread(() =>
        {
            ContentDialog dialog = CreateDialog(options);
            EnqueueDialogInternal(resultSource, windowId, dialog);
        });

        return resultSource.Task;
    }

    public static Task<ContentDialogResult> EnqueueDialogAsync(int windowId, ContentDialog dialog)
    {
        TaskCompletionSource<ContentDialogResult> resultSource = new();
        MainThreadUtils.RunInMainThread(() =>
        {
            EnqueueDialogInternal(resultSource, windowId, dialog);
        });

        return resultSource.Task;
    }

    private static void EnqueueDialogInternal(TaskCompletionSource<ContentDialogResult> resultSource, int windowId, ContentDialog dialog)
    {
        if (!_windowDialogQueue.TryGetValue(windowId, out Queue<PendingDialogItem>? queue))
        {
            if (App.WindowManager.GetWindow(windowId) is null)
            {
                Logger.F(TAG, "EnqueueDialogAsync: Window not found.");
                resultSource.SetResult(ContentDialogResult.None);
                return;
            }

            queue = [];
            _windowDialogQueue[windowId] = queue;
        }

        queue.Enqueue(new()
        {
            Dialog = dialog,
            ResultSource = resultSource,
        });

        if (queue.Count == 1)
        {
            ShowNextDialog(windowId);
        }
    }

    private static async void ShowNextDialog(int windowId)
    {
        if (!_windowDialogQueue.TryGetValue(windowId, out Queue<PendingDialogItem>? queue))
        {
            return;
        }

        void ClearQueue()
        {
            List<PendingDialogItem> queueCopy = [.. queue];
            _windowDialogQueue.Remove(windowId);
            foreach (PendingDialogItem item in queueCopy)
            {
                item.ResultSource.SetResult(ContentDialogResult.None);
            }
        }

        while (queue.TryPeek(out PendingDialogItem? item))
        {
            MainWindow? window = App.WindowManager.GetWindow(windowId);
            if (window is null)
            {
                ClearQueue();
                return;
            }

            XamlRoot? xamlRoot = (window.Content as FrameworkElement)?.XamlRoot;
            if (xamlRoot is null)
            {
                ClearQueue();
                return;
            }

            ContentDialogResult result = ContentDialogResult.None;
            try
            {
                item.Dialog.XamlRoot = xamlRoot;
                result = await item.Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
            }

            item.ResultSource.SetResult(result);
            queue.Dequeue();
        }
    }

    private static ContentDialog CreateDialog(DialogOptions options)
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
        };

        if (!string.IsNullOrEmpty(options.SecondaryButtonText))
        {
            dialog.SecondaryButtonText = options.SecondaryButtonText;
        }

        if (!string.IsNullOrEmpty(options.CloseButtonText))
        {
            dialog.CloseButtonText = options.CloseButtonText;
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

        return dialog;
    }

    public class DialogOptions
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public string PrimaryButtonText { get; private set; } = StringResourceProvider.Instance.OK;
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

    private class PendingDialogItem
    {
        public required ContentDialog Dialog;
        public required TaskCompletionSource<ContentDialogResult> ResultSource;
    }
}
