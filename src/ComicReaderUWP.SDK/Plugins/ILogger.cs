// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins;

public interface ILogger
{
    void D(string tag, string? message);

    void I(string tag, string? message);

    void W(string tag, string? message);

    void E(string tag, string? message, Exception? exception);

    void F(string tag, string? message, Exception? exception);
}
