// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.Views.AppWindows.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Utils;

internal class DialogUtils
{
    private const string TAG = nameof(DialogUtils);

    private static readonly Dictionary<int, Queue<PendingDialogItem>> _windowDialogQueue = [];

    public static Task<DialogResult> EnqueueDialogAsync(DialogOptions options)
    {
        TaskCompletionSource<DialogResult> resultSource = new();
        CoroutineUtils.RunInMainThread(() =>
        {
            MainWindow? window = App.Instance.WindowManager.GetActiveWindow() ?? App.Instance.WindowManager.GetAnyWindow();
            if (window is null)
            {
                Logger.F(TAG, "ShowDialogAtActiveWindowAsync: Window not found");
                resultSource.SetResult(DialogResult.FromFailure());
                return;
            }

            ContentDialog dialog = CreateDialog(options);
            EnqueueDialogInternal(resultSource, window.WindowId, dialog);
        });

        return resultSource.Task;
    }

    public static Task<DialogResult> EnqueueDialogAsync(int windowId, DialogOptions options)
    {
        TaskCompletionSource<DialogResult> resultSource = new();
        CoroutineUtils.RunInMainThread(() =>
        {
            ContentDialog dialog = CreateDialog(options);
            EnqueueDialogInternal(resultSource, windowId, dialog);
        });

        return resultSource.Task;
    }

    public static Task<DialogResult> EnqueueDialogAsync(ContentDialog dialog)
    {
        TaskCompletionSource<DialogResult> resultSource = new();
        CoroutineUtils.RunInMainThread(() =>
        {
            MainWindow? window = App.Instance.WindowManager.GetActiveWindow() ?? App.Instance.WindowManager.GetAnyWindow();
            if (window is null)
            {
                Logger.F(TAG, "ShowDialogAtActiveWindowAsync: Window not found");
                resultSource.SetResult(DialogResult.FromFailure());
                return;
            }

            EnqueueDialogInternal(resultSource, window.WindowId, dialog);
        });

        return resultSource.Task;
    }

    public static Task<DialogResult> EnqueueDialogAsync(int windowId, ContentDialog dialog)
    {
        TaskCompletionSource<DialogResult> resultSource = new();
        CoroutineUtils.RunInMainThread(() =>
        {
            EnqueueDialogInternal(resultSource, windowId, dialog);
        });

        return resultSource.Task;
    }

    private static void EnqueueDialogInternal(TaskCompletionSource<DialogResult> resultSource, int windowId, ContentDialog dialog)
    {
        if (!_windowDialogQueue.TryGetValue(windowId, out Queue<PendingDialogItem>? queue))
        {
            if (App.Instance.WindowManager.GetWindow(windowId) is null)
            {
                Logger.F(TAG, "EnqueueDialogAsync: Window not found");
                resultSource.SetResult(DialogResult.FromFailure());
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
                item.ResultSource.SetResult(DialogResult.FromFailure());
            }
        }

        while (queue.TryPeek(out PendingDialogItem? item))
        {
            MainWindow? window = App.Instance.WindowManager.GetWindow(windowId);
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

            item.ResultSource.SetResult(DialogResult.FromSuccess(result));
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

        string primaryButtonText = options.PrimaryButtonText;
        if (string.IsNullOrEmpty(primaryButtonText))
        {
            primaryButtonText = StringResourceProvider.Instance.OK;
        }

        var dialog = new ContentDialog
        {
            Title = options.Title,
            Content = scrollableContent,
            PrimaryButtonText = primaryButtonText,
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

    private class PendingDialogItem
    {
        public required ContentDialog Dialog;
        public required TaskCompletionSource<DialogResult> ResultSource;
    }
}
