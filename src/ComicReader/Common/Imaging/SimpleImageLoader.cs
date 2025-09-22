// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common.Utils;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.Common.Imaging;

internal static class SimpleImageLoader
{
    public static ITaskDispatcher DefaultDispatcher { get; } = TaskDispatcher.Factory.NewThreadPool("SimpleImageLoader");

    public sealed class Transaction : BaseTransaction
    {
        private readonly CancellationSession.IToken _sessionToken;
        private readonly List<Token> _tokens;
        private ITaskDispatcher _dispatcher = DefaultDispatcher;

        public Transaction(CancellationSession.IToken token, List<Token> tokens)
        {
            _sessionToken = token;
            _tokens = tokens;
        }

        public Transaction SetDispatcher(ITaskDispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(dispatcher);

            _dispatcher = dispatcher;
            return this;
        }

        protected override void CommitImpl()
        {
            _dispatcher.Submit("SimpleImageLoader", delegate
            {
                foreach (Token token in _tokens)
                {
                    double width = token.Width * token.Multiplication;
                    double height = token.Height * token.Multiplication;

                    ImageCacheManager.LoadImage(_sessionToken, token.Source, width, height, token.StretchMode, token.ImageResultHandler);
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
        public double Multiplication { get; set; } = 1.0;
        public IImageResultHandler ImageResultHandler { get; set; } = callback;
    }
}
