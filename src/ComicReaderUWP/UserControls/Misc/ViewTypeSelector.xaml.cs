// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class ViewTypeSelector : BaseUserControl
{
    public delegate void ViewTypeChangedEventHandler(ComicFilterModel.ViewTypeEnum viewType);
    public event ViewTypeChangedEventHandler? ViewTypeChanged;

    private static readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    public ComicFilterModel.ViewTypeEnum SelectedViewType
    {
        get => (ComicFilterModel.ViewTypeEnum)GetValue(SelectedViewTypeProperty);
        set => SetValue(SelectedViewTypeProperty, value);
    }

    public static readonly DependencyProperty SelectedViewTypeProperty = DependencyProperty.Register(
        nameof(SelectedViewType),
        typeof(ComicFilterModel.ViewTypeEnum),
        typeof(ViewTypeSelector),
        new PropertyMetadata(ComicFilterModel.ViewTypeEnum.Medium));

    public ViewTypeSelector()
    {
        InitializeComponent();

        var flyout = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };
        flyout.Opening += (sender, args) => RebuildItems(flyout);
        MainButton.Flyout = flyout;
    }

    private void RebuildItems(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        foreach (ComicFilterModel.ViewTypeEnum viewType in _viewTypes)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = ViewTypeToDisplayName(viewType),
                IsChecked = viewType == SelectedViewType,
            };
            ComicFilterModel.ViewTypeEnum selected = viewType;
            item.Click += (sender, args) => ViewTypeChanged?.Invoke(selected);
            flyout.Items.Add(item);
        }
    }

    private static string ViewTypeToDisplayName(ComicFilterModel.ViewTypeEnum viewType)
    {
        return viewType switch
        {
            ComicFilterModel.ViewTypeEnum.Large => StringResourceProvider.Instance.ViewTypeLarge,
            ComicFilterModel.ViewTypeEnum.Medium => StringResourceProvider.Instance.ViewTypeMedium,
            _ => "Unknown"
        };
    }
}
