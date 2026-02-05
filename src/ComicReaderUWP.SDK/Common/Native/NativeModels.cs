// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Native;

internal class NativeModels
{
    public delegate void WinEventDelegate(nint winEventHookHandle, uint eventType, nint windowHandle, int objectId, int childId, uint eventThreadId, uint eventTimeInMilliseconds);
}
