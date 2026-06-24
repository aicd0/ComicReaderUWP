// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class ReaderImageSettings
{
    public ImageRotationEnum Rotation { get; set; } = ImageRotationEnum.None;
    public bool Flip { get; set; } = false;
    public double AntiAliasingFilterRatio { get; set; } = 0;
    public float Brightness { get; set; } = 0.5F;
    public float Contrast { get; set; } = 0.5F;
    public bool Invert { get; set; } = false;
}
