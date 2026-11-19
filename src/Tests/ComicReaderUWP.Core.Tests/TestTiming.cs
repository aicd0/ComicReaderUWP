// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Tests;

internal static class TestTiming
{
    /// <summary>
    /// Waits for <paramref name="condition"/> to become true, failing the test when it never does. Used where
    /// the observable is a background task's progress rather than a completion.
    /// </summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(message);
            }

            await Task.Delay(20);
        }
    }
}
