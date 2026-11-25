// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Storage;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Imaging;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReaderUWP.UserControls.ChooseImage;

internal sealed partial class ChooseImageView : BaseUserControl
{
    private const string TAG = nameof(ChooseImageView);
    private const string TEMPORARY_FOLDER_NAME = "ChooseImage";

    private static readonly string[] IMAGE_FILE_TYPES = [.. AppInfoProvider.SupportedImageExtensions];

    private static string FormatImageMeta(ImageMeta meta)
    {
        StringBuilder sb = new();
        sb.Append(meta.Format);
        sb.Append(' ').Append(meta.Width).Append(" x ").Append(meta.Height);
        sb.Append(' ').Append(FormatBytes(meta.Size));

        if (meta.DpiX > 0 && meta.DpiY > 0)
        {
            sb.Append(' ').Append(FormatDpi(meta.DpiX, meta.DpiY));
        }

        sb.Append(' ').Append(meta.BitsPerPixel).Append(" bits");
        return sb.ToString();
    }

    private static string FormatDpi(double dpiX, double dpiY)
    {
        if (dpiX == dpiY)
        {
            return $"{dpiX:0.##} dpi";
        }

        return $"{dpiX:0.##} x {dpiY:0.##} dpi";
    }

    private static string FormatBytes(long byteCount)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        int unitIndex = (int)Math.Floor(Math.Log(byteCount, 1024));
        double adjustedSize = byteCount / Math.Pow(1024, unitIndex);
        return $"{adjustedSize:0.#} {units[unitIndex]}";
    }

    private readonly CancellationSession _metaSession = new();

    private string? _pendingFilePath = null;

    public ChooseImageView()
    {
        InitializeComponent();
    }

    public event EventHandler? Changed;

    public int WindowId { get; set; } = 0;

    public string? PendingFilePath => _pendingFilePath;

    public void Initialize(ResourceUri? value)
    {
        UpdatePreview(value?.ToString());
    }

    private void UpdatePreview(string? uri)
    {
        ImageHolder.ImageUri = uri;

        bool hasImage = !string.IsNullOrEmpty(uri);
        ImageBorder.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderBorder.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;

        UpdateImageInfo(uri);
    }

    private void UpdateImageInfo(string? uri)
    {
        CancellationSession.IToken token = _metaSession.Next();

        ImageInfoTextBlock.Text = string.Empty;
        ImageInfoTextBlock.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            IImageSource? source = await ResolveImageSource(uri);
            if (source is null || token.IsCancellationRequested)
            {
                return;
            }

            ImageMeta? meta = await ImageLoader.LoadImageMeta(source, new()
            {
                Token = token,
            });
            if (meta is null || token.IsCancellationRequested)
            {
                return;
            }

            ImageInfoTextBlock.Text = FormatImageMeta(meta);
            ImageInfoTextBlock.Visibility = Visibility.Visible;
        });
    }

    private void AddImageButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            StorageFile? file = await FilePickerUtils.PickFile(WindowId, IMAGE_FILE_TYPES);
            if (file is null)
            {
                return;
            }

            SetPendingFile(file.Path);
        });
    }

    private void DeleteImageButton_Click(object sender, RoutedEventArgs e)
    {
        SetPendingFile(null);
    }

    private void ImageDropTarget_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement target)
        {
            return;
        }

        args.Handled = true;

        List<BaseMenuFlyoutItemModel> menuItems =
        [
            new SimpleMenuFlyoutItemModel
            {
                Text = StringResourceProvider.Instance.Copy,
                Icon = new FontIconSource { Glyph = "\uE8C8" },
                IsEnabled = !string.IsNullOrEmpty(ImageHolder.ImageUri),
                Click = () => CoroutineUtils.Run(CopyImageAsync),
            },
            new SimpleMenuFlyoutItemModel
            {
                Text = StringResourceProvider.Instance.Paste,
                Icon = new FontIconSource { Glyph = "\uE77F" },
                Click = () => CoroutineUtils.Run(PasteImageAsync),
            },
        ];

        MenuFlyout flyout = new();
        foreach (BaseMenuFlyoutItemModel menuItem in menuItems)
        {
            flyout.Items.Add(menuItem.CreateMenuFlyoutItem());
        }

        if (args.TryGetPosition(target, out Point point))
        {
            flyout.ShowAt(target, new FlyoutShowOptions { Position = point });
        }
        else
        {
            flyout.ShowAt(target);
        }
    }

    private void ImageDropTarget_DragOver(object sender, DragEventArgs args)
    {
        if (args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            args.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private void ImageDropTarget_Drop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();
        args.Handled = true;
        CoroutineUtils.Run(async () =>
        {
            try
            {
                await HandleDropAsync(args);
            }
            catch (Exception exception)
            {
                Logger.W(TAG, $"What was dropped could not be read: {exception.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        });
    }

    private async Task HandleDropAsync(DragEventArgs args)
    {
        IReadOnlyList<IStorageItem> items = await args.DataView.GetStorageItemsAsync();
        string? filePath = await FindImageFilePathAsync(items);
        if (filePath is not null)
        {
            SetPendingFile(filePath);
        }
    }

    private async Task CopyImageAsync()
    {
        string? uri = ImageHolder.ImageUri;
        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        IImageSource? source = await ResolveImageSource(uri);
        if (source is null)
        {
            Logger.W(TAG, $"The image '{uri}' could not be resolved, so it was not copied.");
            return;
        }

        using ImageContent? content = await ImageLoader.LoadImageContent(source, new());
        if (content is null)
        {
            Logger.W(TAG, $"The image '{uri}' could not be read, so it was not copied.");
            return;
        }

        OperationResult result = await ClipboardUtils.SetImage(content.Stream);
        if (!result.IsSuccessful)
        {
            Logger.W(TAG, $"The image '{uri}' could not be put into the clipboard: {result.ErrorMessage}");
        }
    }

    private async Task PasteImageAsync()
    {
        DataPackageView content;
        try
        {
            content = Clipboard.GetContent();
        }
        catch (Exception exception)
        {
            Logger.W(TAG, $"The clipboard could not be read: {exception.Message}");
            return;
        }

        try
        {
            if (content.Contains(StandardDataFormats.StorageItems))
            {
                string? filePath = await FindImageFilePathAsync(await content.GetStorageItemsAsync());
                if (filePath is not null)
                {
                    SetPendingFile(filePath);
                    return;
                }
            }

            if (!content.Contains(StandardDataFormats.Bitmap))
            {
                Logger.W(TAG, "The clipboard holds no image, so nothing was pasted.");
                return;
            }

            string? bitmapFilePath = await SaveClipboardBitmapAsync(content);
            if (bitmapFilePath is not null)
            {
                SetPendingFile(bitmapFilePath);
            }
        }
        catch (Exception exception)
        {
            Logger.W(TAG, $"What was in the clipboard could not be read: {exception.Message}");
        }
    }

    private static async Task<IImageSource?> ResolveImageSource(string uri)
    {
        if (ResourceUri.TryParse(uri, out ResourceUri? resourceUri))
        {
            return await resourceUri.ResolveImage();
        }

        return File.Exists(uri) ? new LocalFileImageSource(uri) : null;
    }

    private static async Task<string?> FindImageFilePathAsync(IReadOnlyList<IStorageItem> items)
    {
        foreach (IStorageItem item in items)
        {
            if (item is not StorageFile file)
            {
                Logger.W(TAG, $"'{item.Name}' is not a file, so it was ignored.");
                continue;
            }

            string? filePath = await GetImageFilePathAsync(file);
            if (filePath is not null)
            {
                return filePath;
            }
        }

        return null;
    }

    private static async Task<string?> GetImageFilePathAsync(StorageFile file)
    {
        string extension = file.FileType.ToLower();
        if (!AppInfoProvider.SupportedImageExtensions.Contains(extension))
        {
            Logger.W(TAG, $"'{file.Name}' is not a supported image, so it was ignored.");
            return null;
        }

        if (!string.IsNullOrEmpty(file.Path))
        {
            return file.Path;
        }

        // A file dropped from a browser is a virtual item that has content but no path,
        // so its content has to become a temporary file for the rest of the flow to read it.
        try
        {
            string filePath = CreateTemporaryFilePath(extension);
            using Stream stream = await file.OpenStreamForReadAsync();
            using FileStream fileStream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
            return filePath;
        }
        catch (Exception exception)
        {
            Logger.W(TAG, $"'{file.Name}' could not be copied to a temporary file: {exception.Message}");
            return null;
        }
    }

    private static async Task<string?> SaveClipboardBitmapAsync(DataPackageView content)
    {
        try
        {
            RandomAccessStreamReference? reference = await content.GetBitmapAsync();
            if (reference is null)
            {
                return null;
            }

            using InMemoryRandomAccessStream buffer = new();
            using (IRandomAccessStreamWithContentType sourceStream = await reference.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, buffer);
                encoder.SetSoftwareBitmap(bitmap);
                await encoder.FlushAsync();
            }

            string filePath = CreateTemporaryFilePath(".png");
            buffer.Seek(0);
            using FileStream fileStream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using Stream stream = buffer.AsStreamForRead();
            await stream.CopyToAsync(fileStream);
            return filePath;
        }
        catch (Exception exception)
        {
            Logger.W(TAG, $"The clipboard image could not be saved: {exception.Message}");
            return null;
        }
    }

    private static string CreateTemporaryFilePath(string extension)
    {
        string directoryPath = Path.Combine(StorageLocation.TemporaryFolderPath, TEMPORARY_FOLDER_NAME);
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, Guid.NewGuid().ToString("N") + extension);
    }

    private void SetPendingFile(string? filePath)
    {
        _pendingFilePath = filePath;
        UpdatePreview(filePath);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
