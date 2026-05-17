// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.UserControls.Reader.PageLayout;

namespace ComicReaderUWP.Tests;

public class SpreadDetectionTest
{
    [Test]
    public void TestSpreadDetection()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(IsSpreadPage(1.0F, [1.0F]), Is.False);
            Assert.That(IsSpreadPage(1.1F, [0.9F, 1.1F]), Is.False);
            Assert.That(IsSpreadPage(0.9F, [0.9F, 1.5F]), Is.False);
            Assert.That(IsSpreadPage(1.5F, [0.9F, 1.5F]), Is.False); // Samples too few
            Assert.That(IsSpreadPage(1.4F, [0.7F, 1.0F, 1.4F]), Is.False);
            Assert.That(IsSpreadPage(1.0F, [0.7F, 1.0F, 1.6F]), Is.False);
            Assert.That(IsSpreadPage(1.6F, [0.7F, 1.0F, 1.6F]), Is.True);
            Assert.That(IsSpreadPage(1.5F, [0.6F, 0.7F, 1.5F, 1.6F, 1.7F]), Is.True);
            Assert.That(IsSpreadPage(1.5F, [0.4F, 0.5F, 0.6F, 0.7F, 1.5F, 1.6F, 1.7F]), Is.True);
            Assert.That(IsSpreadPage(0.3F, [0.1F, 0.2F, 0.3F]), Is.False); // Too narrow
            Assert.That(IsSpreadPage(1.6F, [0.3F, 0.4F, 0.5F, 1.5F, 1.6F]), Is.False); // Too narrow for previous group
        }
    }

    private static bool IsSpreadPage(float aspectRatio, IEnumerable<float> samples)
    {
        SpreadDetectionHelper.PageSamples pageSamples = new();
        foreach (float item in samples)
        {
            pageSamples.Add(item);
        }

        return pageSamples.IsSpreadPage(aspectRatio);
    }
}
