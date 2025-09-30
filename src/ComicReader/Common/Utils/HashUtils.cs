// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Text;

namespace ComicReader.Common.Utils;

internal static class HashUtils
{
    internal static int GetSHA256Int(int input)
    {
        byte[] bytes = BitConverter.GetBytes(input);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        byte[] hash = SHA256.HashData(bytes);
        return (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3];
    }

    internal static int GetSHA256Int(long input)
    {
        byte[] bytes = BitConverter.GetBytes(input);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        byte[] hash = SHA256.HashData(bytes);
        return (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3];
    }

    internal static int GetSHA256Int(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        return (hash[0] << 24) | (hash[1] << 16) | (hash[2] << 8) | hash[3];
    }
}
