// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Test;

public static class SpreadDetectionHelper
{
    public class PageSamples
    {
        private const float MIN_ASPECT_RATIO = 0.6F;

        private readonly SortedDictionary<float, int> _aspectRatios = [];
        private int _aspectRatiosCount = 0;

        public int Count => _aspectRatiosCount;

        public void Clear()
        {
            _aspectRatiosCount = 0;
            _aspectRatios.Clear();
        }

        public void Add(float aspectRatio)
        {
            _aspectRatiosCount++;
            if (_aspectRatios.TryGetValue(aspectRatio, out int count))
            {
                _aspectRatios[aspectRatio] = count + 1;
            }
            else
            {
                _aspectRatios[aspectRatio] = 1;
            }
        }

        public bool IsSpreadPage(float aspectRatio)
        {
            if (Count <= 2)
            {
                return false;
            }

            if (aspectRatio < MIN_ASPECT_RATIO)
            {
                // Too narrow to be a spread page
                return false;
            }

            float previous = -1F;
            float threshold = -1F;
            foreach (KeyValuePair<float, int> kvp in _aspectRatios)
            {
                float current = kvp.Key;

                if (previous >= MIN_ASPECT_RATIO &&
                    current >= MIN_ASPECT_RATIO &&
                    current / previous >= 1.5F)
                {
                    threshold = (previous + current) * 0.5F;
                    break;
                }

                previous = current;
            }

            if (threshold < 0)
            {
                return false;
            }

            return aspectRatio > threshold;
        }
    }
}
