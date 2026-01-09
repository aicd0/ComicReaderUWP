// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Database.SqlHelpers;

public static class SqlUtils
{
    public static IEnumerable<IEnumerable<T>> ChunkBy<T>(IEnumerable<T> source, int chunkSize = 900)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSize));
        }

        List<List<T>> chunks = [];
        List<T> chunk = [];
        foreach (T item in source)
        {
            chunk.Add(item);
            if (chunk.Count >= chunkSize)
            {
                chunks.Add(chunk);
                chunk = [];
            }
        }

        if (chunk.Count > 0)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }
}
