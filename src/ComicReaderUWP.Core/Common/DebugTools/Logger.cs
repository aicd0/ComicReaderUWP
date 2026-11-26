// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;

using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class Logger
{
    private const string TAG = nameof(Logger);

    private const int FLUSH_INTERVAL = 5000;
    private const int BUFFER_SIZE = 1000;
    private const string EMPTY_TAG = "Empty";

    private static int sNextLogId = 0;
    private static int sPostDispatch = 0;
    private static readonly ConcurrentQueue<LogEntry> sPendingQueue = new();
    private static readonly LinkedList<LogEntry> sBuffer = new();
    private static readonly ConcurrentDictionary<ILogListener, LogListenerWrapper> sListeners = [];

    private static int sInitialized = 0;
    private static string sLogFolderPath = string.Empty;
    private static string sLogTreeFolderPath = string.Empty;
    private static readonly ConcurrentQueue<LogEntry> sFlushQueue = new();
    private static long sLastErrorReportTime = 0;

    private static bool Initialized => sInitialized == 1;

    public static void Initialize()
    {
        if (Interlocked.CompareExchange(ref sInitialized, 1, 0) != 0)
        {
            F(TAG, "Multiple initializations");
            return;
        }

        sLogFolderPath = StorageLocation.LocalCacheFolderPath + "\\Logs\\";
        sLogTreeFolderPath = sLogFolderPath + "Tree\\";

        Thread logThread = new(LogThreadMain)
        {
            Name = "Logger",
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
        };

        logThread.Start();
        AddListener(new InternalLogListener());
    }

    public static void AddListener(ILogListener listener)
    {
        if (listener == null)
        {
            return;
        }

        LogListenerWrapper wrapper = new()
        {
            Listener = listener,
        };

        if (sListeners.TryAdd(listener, wrapper))
        {
            DispatchLogItems();
        }
    }

    public static void RemoveListener(ILogListener listener)
    {
        if (listener == null)
        {
            return;
        }

        sListeners.TryRemove(listener, out _);
    }

    public static void Flush()
    {
        if (!Initialized)
        {
            return;
        }

        FlushToFile();
    }

    public static bool IsTagMatch(string filter, string tag)
    {
        if (tag.Equals(filter, StringComparison.Ordinal))
        {
            return true;
        }

        return tag.Length > filter.Length
            && tag[filter.Length] == '/'
            && tag.StartsWith(filter, StringComparison.Ordinal);
    }

    public static string[] GetLogTreeFilePaths(string? tag)
    {
        if (!Initialized)
        {
            return [];
        }

        return LogTree.GetFilePaths(sLogTreeFolderPath, tag ?? string.Empty);
    }

    public static void D(string? tag, string? message)
    {
        Log(LogLevel.Debug, tag, message, null);
    }

    public static void I(string? tag, string? message)
    {
        Log(LogLevel.Info, tag, message, null);
    }

    public static void W(string? tag, string? message)
    {
        Log(LogLevel.Warning, tag, message, null);
    }

    public static void E(string? tag, string? message)
    {
        Log(LogLevel.Error, tag, message, null);
    }

    public static void E(string? tag, Exception? exception)
    {
        Log(LogLevel.Error, tag, null, exception);
    }

    public static void E(string? tag, string? message, Exception? exception)
    {
        Log(LogLevel.Error, tag, message, exception);
    }

    public static void F(string? tag, string? message)
    {
        AssertException exceptionNotNull = new(message);
        Log(LogLevel.Fatal, tag, message, exceptionNotNull);
    }

    public static void F(string? tag, Exception? exception)
    {
        AssertException exceptionNotNull = new(null, exception);
        Log(LogLevel.Fatal, tag, null, exceptionNotNull);
    }

    public static void F(string? tag, string? message, Exception? exception)
    {
        AssertException exceptionNotNull = new(message, exception);
        Log(LogLevel.Fatal, tag, message, exceptionNotNull);
    }

    public static void Assert(bool condition, string? message)
    {
        if (condition)
        {
            return;
        }

        AssertNotReachHereInternal(message, null);
    }

    public static void AssertNotReachHere(string? message)
    {
        AssertNotReachHereInternal(message, null);
    }

    private static void AssertNotReachHereInternal(string? message, Exception? exception)
    {
        AssertException exceptionNotNull = new(message, exception);
        Log(LogLevel.Fatal, "Assert", message, exceptionNotNull);
    }

    private static void Console(string message)
    {
        Log(LogLevel.Console, TAG, message, null);
    }

    private static void LogThreadMain()
    {
        Console("Logger initialized");

        while (true)
        {
            FlushToFile();
            Thread.Sleep(FLUSH_INTERVAL);
        }
    }

    private static void Log(LogLevel level, string? tag, string? message, Exception? exception)
    {
        tag ??= EMPTY_TAG;
        var item = new LogEntry
        {
            Id = Interlocked.Increment(ref sNextLogId) - 1,
            Time = DateTimeOffset.Now,
            Level = level,
            Tag = tag,
            Message = message,
            Exception = exception
        };

        sPendingQueue.Enqueue(item);
        DispatchLogItems();

        if (item.Level >= LogLevel.Fatal)
        {
            if (item.Exception is AssertException assertException)
            {
                FailOnDebug(assertException);
            }
            else
            {
                FailOnDebug(new AssertException("Expect an AssertException.", item.Exception));
            }
        }
    }

    private static void DispatchLogItems()
    {
        bool postDispatch = Interlocked.Exchange(ref sPostDispatch, 1) == 0;
        if (!postDispatch)
        {
            return;
        }

        TaskDispatcher.DefaultQueue.Submit(() =>
        {
            Interlocked.Exchange(ref sPostDispatch, 0);
            List<LogEntry> pendingLogs = [];
            while (sPendingQueue.TryDequeue(out LogEntry? item))
            {
                pendingLogs.Add(item);
            }

            foreach (LogEntry item in pendingLogs)
            {
                sBuffer.AddLast(item);
            }

            List<LogListenerWrapper> snapshot = [.. sListeners.Values];
            List<LogEntry> dispatchingItems = [];
            foreach (LogListenerWrapper listener in snapshot)
            {
                dispatchingItems.Clear();
                for (LinkedListNode<LogEntry>? node = sBuffer.Last; node != null; node = node.Previous)
                {
                    LogEntry item = node.Value;
                    if (item.Id <= listener.LastId)
                    {
                        break;
                    }

                    dispatchingItems.Add(item);
                }

                for (int i = dispatchingItems.Count - 1; i >= 0; i--)
                {
                    LogEntry item = dispatchingItems[i];
                    listener.LastId = item.Id;
                    listener.Listener.OnLog(item);
                }
            }

            while (sBuffer.Count > BUFFER_SIZE)
            {
                sBuffer.RemoveFirst();
            }
        });
    }

    private static void FlushToFile()
    {
        if (sFlushQueue.IsEmpty)
        {
            return;
        }

        List<LogEntry> logs = [];
        while (true)
        {
            if (sFlushQueue.TryDequeue(out LogEntry? item))
            {
                logs.Add(item);
            }
            else
            {
                break;
            }
        }

        FlushToLogFile(logs);
        FlushToLogTree(logs);
    }

    private static void FlushToLogFile(List<LogEntry> logs)
    {
        StringBuilder sb = new();
        foreach (LogEntry item in logs)
        {
            sb.Append(item.DisplayMessage);
            sb.Append('\n');
        }

        string content = sb.ToString();

        string fileName = "log_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
        string filePath = sLogFolderPath + fileName;
        try
        {
            Directory.CreateDirectory(sLogFolderPath);
            using StreamWriter writer = new(filePath, true, Encoding.UTF8);
            writer.Write(content);
            Console($"flushed {logs.Count} logs to {filePath}");
        }
        catch (Exception ex)
        {
            F(TAG, ex);
        }
    }

    private static void FlushToLogTree(List<LogEntry> logs)
    {
        LogTree.Append(sLogTreeFolderPath, logs);
    }

    private static void LogToConsole(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }

    private static void FailOnDebug(AssertException exception)
    {
        long time = GetTick();
        if (time - sLastErrorReportTime > 5000)
        {
            sLastErrorReportTime = time;
            SentryManager.CaptureWarning(exception);
        }

        if (DebugUtils.DebugMode)
        {
            CrashHandler.OnCrash(exception);
        }
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }

    private class InternalLogListener : ILogListener
    {
        public void OnLog(LogEntry item)
        {
            if (DebugModel.ConsoleEnabled)
            {
                LogToConsole(item.DisplayMessage);
            }

            if (DebugUtils.DebugMode && item.Level >= LogLevel.Info)
            {
                sFlushQueue.Enqueue(item);
            }
        }
    }

    private class AssertException : Exception
    {
        public AssertException(string? message) : base(ConvertMessage(message)) { }

        public AssertException(string? message, Exception? inner) : base(ConvertMessage(message), inner) { }

        private static string ConvertMessage(string? message)
        {
            return message ?? "(no message)";
        }
    }

    private class LogListenerWrapper
    {
        public int LastId = -1;
        public required ILogListener Listener { init; get; }
    }

    public interface ILogListener
    {
        void OnLog(LogEntry item);
    }
}
