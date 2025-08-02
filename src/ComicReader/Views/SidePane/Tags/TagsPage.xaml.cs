// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Lifecycle;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;

namespace ComicReader.Views.SidePane.Tags;

internal sealed partial class TagsPage : BasePage
{
    private readonly TagsPageViewModel ViewModel = new();

    public TagsPage()
    {
        InitializeComponent();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
        ViewModel.UpdateTags();
    }

    private void ObserveData()
    {
        EventBus.Default.With(EventId.TagsUpdated).Observe(this, delegate
        {
            ViewModel.UpdateTags();
        });
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }
}
