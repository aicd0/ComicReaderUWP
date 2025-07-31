// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace ComicReader.Common.Legacy;

internal class C0
{
    public static void Run(Action action)
    {
        action();
    }

    public static async Task WaitFor(Func<bool> signal, int timeout_milliseconds = -1)
    {
        DateTimeOffset start_time = DateTimeOffset.Now;

        await Task.Run(delegate
        {
            var sw = new SpinWait();

            while (!signal())
            {
                if (timeout_milliseconds >= 0)
                {
                    TimeSpan time_elapsed = DateTimeOffset.Now - start_time;

                    if ((int)time_elapsed.TotalMilliseconds > timeout_milliseconds)
                    {
                        break;
                    }
                }

                sw.SpinOnce();
            }
        });
    }

    public static IBuffer GetBufferFromString(string text)
    {
        if (text.Length == 0)
        {
            return new Windows.Storage.Streams.Buffer(0);
        }
        else
        {
            return CryptographicBuffer.ConvertStringToBinary(
                text, BinaryStringEncoding.Utf8);
        }
    }

    public static Stream GetStreamFromString(string text)
    {
        IBuffer buffer = GetBufferFromString(text);
        return buffer.AsStream();
    }
}

interface IC1<out T> { }
public class C1<T> : IC1<T>
{
    public class DefaultEqualityComparer : EqualityComparer<T>
    {
        public override bool Equals(T x, T y)
        {
            return x.Equals(y);
        }

        public override int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }

    public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
    {
        int idx = collection.IndexOf(item);
        collection.Insert(idx + 1, item);
        collection.RemoveAt(idx);
    }
}

public interface IC3<out T, out U, out V> { }
public class C3<T, U, V> : IC3<T, U, V>
{
    private class KeyEqualityComparer : EqualityComparer<KeyValuePair<V, object>>
    {
        readonly IEqualityComparer<V> m_comparer;

        public KeyEqualityComparer(IEqualityComparer<V> comparer)
        {
            m_comparer = comparer;
        }

        public override bool Equals(KeyValuePair<V, object> x, KeyValuePair<V, object> y)
        {
            return m_comparer.Equals(x.Key, y.Key);
        }

        public override int GetHashCode(KeyValuePair<V, object> obj)
        {
            return m_comparer.GetHashCode(obj.Key);
        }
    }

    public static IEnumerable<T> Except(IEnumerable<T> first, IEnumerable<U> second,
        Func<T, V> key_first, Func<U, V> key_second, IEqualityComparer<V> comparer)
    {
        var pairs_1 = new List<KeyValuePair<V, object>>();
        var pairs_2 = new List<KeyValuePair<V, object>>();

        foreach (T val in first)
        {
            pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
        }

        foreach (U val in second)
        {
            pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
        }

        IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Except(pairs_2, new KeyEqualityComparer(comparer));
        var output = new List<T>();

        foreach (KeyValuePair<V, object> val in processed)
        {
            output.Add((T)val.Value);
        }

        return output;
    }

    public static IEnumerable<T> Intersect(IEnumerable<T> first, IEnumerable<U> second,
        Func<T, V> key_first, Func<U, V> key_second, IEqualityComparer<V> comparer)
    {
        var pairs_1 = new List<KeyValuePair<V, object>>();
        var pairs_2 = new List<KeyValuePair<V, object>>();

        foreach (T val in first)
        {
            pairs_1.Add(new KeyValuePair<V, object>(key_first(val), val));
        }

        foreach (U val in second)
        {
            pairs_2.Add(new KeyValuePair<V, object>(key_second(val), val));
        }

        IEnumerable<KeyValuePair<V, object>> processed = pairs_1.Intersect(pairs_2, new KeyEqualityComparer(comparer));
        var output = new List<T>();

        foreach (KeyValuePair<V, object> val in processed)
        {
            output.Add((T)val.Value);
        }

        return output;
    }
}
