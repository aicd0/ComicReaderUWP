// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Storage;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.UI.Xaml;

using Windows.Storage;

namespace ComicReaderUWP.UserControls.ChooseImage;

internal sealed partial class ChooseImageView : BaseUserControl
{
    private static readonly string[] IMAGE_FILE_TYPES = [.. AppInfoProvider.SupportedImageExtensions];

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
        ImageHolder.Uri = uri;

        bool hasImage = !string.IsNullOrEmpty(uri);
        ImageBorder.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderBorder.Visibility = hasImage ? Visibility.Collapsed : Visibility.Visible;
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

            _pendingFilePath = file.Path;
            UpdatePreview(file.Path);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private void DeleteImageButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingFilePath = null;
        UpdatePreview(null);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
