// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Caching;

public abstract class LRUCacheStream : Stream
{
    protected readonly Stream Inner;

    protected LRUCacheStream(Stream inner)
    {
        Inner = inner;
    }

    public override bool CanRead => Inner.CanRead;

    public override bool CanSeek => Inner.CanSeek;

    public override bool CanWrite => Inner.CanWrite;

    public override long Length => Inner.Length;

    public override long Position { get => Inner.Position; set => Inner.Position = value; }

    public override void Flush()
    {
        Inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return Inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        Inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Inner.Write(buffer, offset, count);
    }
}
