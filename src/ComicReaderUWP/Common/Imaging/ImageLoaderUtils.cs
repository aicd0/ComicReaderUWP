// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Common.Imaging;

internal static class ImageLoaderUtils
{
    public sealed class Transaction(CancellationSession.IToken token, List<Token> tokens) : BaseTransaction
    {
        private readonly CancellationSession.IToken _sessionToken = token;
        private readonly List<Token> _tokens = tokens;

        protected override void CommitImpl()
        {
            CoroutineUtils.Run(async () =>
            {
                foreach (Token token in _tokens)
                {
                    LoadImageOptions options = new()
                    {
                        Token = _sessionToken,
                        FrameWidth = token.Width,
                        FrameHeight = token.Height,
                        StretchMode = token.StretchMode,
                        Handler = token.ImageResultHandler,
                    };
                    await ImageLoader.LoadImage(token.Source, options);
                }
            });
        }
    }

    public class Token(IImageSource source, IImageResultHandler callback)
    {
        public IImageSource Source { get; set; } = source;
        public double Width { get; set; } = double.PositiveInfinity;
        public double Height { get; set; } = double.PositiveInfinity;
        public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;
        public IImageResultHandler ImageResultHandler { get; set; } = callback;
    }
}
