// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Localization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.BaseUI;

public partial class BaseUserControl : UserControl
{
    private bool _isLoaded = false;

    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;

    public BaseUserControl()
    {
        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _isLoaded)
        {
            return;
        }

        _isLoaded = true;
        OnResume();
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        if (IsLoaded || !_isLoaded)
        {
            return;
        }

        _isLoaded = false;
        OnPause();
    }

    protected virtual void OnResume()
    {
    }

    protected virtual void OnPause()
    {
    }
}
