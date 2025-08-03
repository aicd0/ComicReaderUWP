// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ComicReader.ViewModels;

public class TagViewModel
{
    public string Tag { get; set; } = string.Empty;

    public Action? OnClicked { get; set; }
};

public class TagCollectionViewModel(string name)
{
    public string Name { get; set; } = name;
    public List<TagViewModel> Tags { get; set; } = [];
};
