// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Lifecycle.Utils;
using ComicReader.SDK.Common.Test;
using ComicReader.SDK.Common.Utils;

namespace ComicReader.Tests.Lifecycle;

[TestFixture]
internal class LiveDataTest
{
    [Test]
    public void TestMutableLiveDataWithMinInterval()
    {
        TestSettings.UseCurrentThreadAsMainThread = true;

        MutableLiveData<int> internalLiveData = new();
        MutableLiveDataWithMinInterval<int> liveData = new(internalLiveData, 1000);

        async Task Test()
        {
            int observedValue = 0;
            int changeCount = 0;
            AlwaysActiveLifecycleOwner owner = new();
            for (int i = 0; i < 100; i++)
            {
                int temp = i;
                liveData.Observe(owner, delegate (int value)
                {
                    Interlocked.Increment(ref changeCount);
                    observedValue = value;
                });
            }

            // first emission should be immediate
            liveData.Emit(1);
            Assert.Multiple(() =>
            {
                Assert.That(observedValue, Is.EqualTo(1));
                Assert.That(changeCount, Is.EqualTo(100));
            });

            // subsequent emissions should be throttled
            for (int i = 2; i <= 100; i++)
            {
                liveData.Emit(i);
                Assert.Multiple(() =>
                {
                    Assert.That(observedValue, Is.EqualTo(1));
                    Assert.That(changeCount, Is.EqualTo(100));
                });
            }

            // now we should see the last value after the delay
            await Task.Delay(2000);
            Assert.Multiple(() =>
            {
                Assert.That(observedValue, Is.EqualTo(100));
                Assert.That(changeCount, Is.EqualTo(200));
            });

            // pressure test with multiple emissions
            changeCount = 0;
            for (int i = 1; i <= 5; i++)
            {
                for (int j = 1; j <= 10; j++)
                {
                    liveData.Emit(i * j);
                    await Task.Delay(1);
                    Assert.Multiple(() =>
                    {
                        Assert.That(observedValue, Is.EqualTo(i));
                        Assert.That(changeCount, Is.EqualTo(i * 200 - 100));
                    });
                }

                await Task.Delay(2000);
                Assert.Multiple(() =>
                {
                    Assert.That(observedValue, Is.EqualTo(i * 10));
                    Assert.That(changeCount, Is.EqualTo(i * 200));
                });
            }
        }

        Test().Wait();
    }

    //private static void PrintCurrentTime(string eventName)
    //{
    //    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {eventName}");
    //}

    private class AlwaysActiveLifecycleOwner : ILifecycleOwner
    {
        private readonly SimpleLifecycle _lifecycle = new();

        public AlwaysActiveLifecycleOwner()
        {
            _lifecycle.SetState(ILifecycle.State.Resumed);
        }

        public ILifecycle GetLifecycle()
        {
            return _lifecycle;
        }
    }
}
