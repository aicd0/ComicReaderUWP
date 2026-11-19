// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Imaging;

public interface IImageResultHandler
{
    public void OnSuccess(DecodedImageModel result);

    public void OnFailure();
}
