// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReaderUWP.Core.Common.Lifecycle;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class MemoryLeakTracker
{
    private static readonly List<WeakReference<ILifecycleOwner>> _trackingObjects = [];

    public static void TrackObject(ILifecycleOwner owner)
    {
        if (DebugUtils.DeveloperMode)
        {
            _trackingObjects.Add(new(owner));
        }
    }

    public static string GenerateReport()
    {
        GC.Collect();

        List<ILifecycleOwner> leakedObjects = [];
        List<ILifecycleOwner> activeObjects = [];

        for (int i = _trackingObjects.Count - 1; i >= 0; i--)
        {
            WeakReference<ILifecycleOwner> ownerRef = _trackingObjects[i];
            if (!ownerRef.TryGetTarget(out ILifecycleOwner? owner))
            {
                _trackingObjects.RemoveAt(i);
                continue;
            }

            ILifecycle.State state = owner.GetLifecycle().GetState();

            if (state == ILifecycle.State.Stopped)
            {
                leakedObjects.Add(owner);
            }
            else
            {
                activeObjects.Add(owner);
            }
        }

        StringBuilder sb = new();

        sb.Append("Leaked objects:").AppendLine();
        foreach (ILifecycleOwner owner in leakedObjects)
        {
            AppendOwner(sb, owner);
        }

        sb.Append("Active objects:").AppendLine();
        foreach (ILifecycleOwner owner in activeObjects)
        {
            AppendOwner(sb, owner);
        }

        return sb.ToString();
    }

    private static StringBuilder AppendOwner(StringBuilder sb, ILifecycleOwner owner)
    {
        return sb.Append(owner.GetType().Name).Append(" (").Append(owner.GetLifecycle().GetState()).Append(')').AppendLine();
    }
}
