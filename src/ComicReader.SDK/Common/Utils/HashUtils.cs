// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace ComicReader.SDK.Common.Utils;

public static class HashUtils
{
    public static byte[] GetXxHash64(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        return GetXxHash64(bytes);
    }

    public static byte[] GetXxHash64(ReadOnlySpan<byte> bytes)
    {
        return XxHash64.Hash(bytes);
    }

    public static int GetXxHash64Int(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        return GetXxHash64Int(buffer);
    }

    public static int GetXxHash64Int(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        return GetXxHash64Int(bytes);
    }

    public static int GetXxHash64Int(ReadOnlySpan<byte> bytes)
    {
        ulong hash = XxHash64.HashToUInt64(bytes);
        return (int)(hash ^ (hash >> 32));
    }
}
