// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;

using ComicReader.SDK.Common.Storage;

namespace ComicReader.SDK.Common.DebugTools;

public static class Logger
{
    private const string TAG = "Logger";
    private const int LEVEL_CONSOLE = 0;
    private const int LEVEL_DEBUG = 1;
    private const int LEVEL_INFO = 2;
    private const int LEVEL_WARN = 3;
    private const int LEVEL_ERROR = 4;
    private const int LEVEL_FATAL = 5;
    private const int LOG_INTERVAL = 5000;

    private static int sInitialized = 0;
    private static string sLogFolderPath = "";
    private static readonly ConcurrentQueue<LogItem> sBuffer = new();
    private static long sLastErrorReportTime = 0;
    private static ImmutableList<ILogListener> sListeners = [];

    private static bool Initialized => sInitialized == 1;

    public static void Initialize()
    {
        if (Interlocked.CompareExchange(ref sInitialized, 1, 0) != 0)
        {
            F(TAG, "Multiple initializations");
            return;
        }

        sLogFolderPath = StorageLocation.LocalCacheFolderPath + "\\logs\\";

        Thread logThread = new(LogThreadMain)
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
        };

        logThread.Start();
    }

    public static void AddListener(ILogListener listener)
    {
        if (listener == null)
        {
            return;
        }

        sListeners = sListeners.Add(listener);
    }

    public static void RemoveListener(ILogListener listener)
    {
        if (listener == null)
        {
            return;
        }

        sListeners = sListeners.Remove(listener);
    }

    public static void Flush()
    {
        if (!Initialized)
        {
            return;
        }

        FlushToFile();
    }

    public static void D(string? tag, string? message)
    {
        Log(LEVEL_DEBUG, LogTag.N(tag), message, null);
    }

    public static void D(LogTag? tag, string? message)
    {
        Log(LEVEL_DEBUG, tag, message, null);
    }

    public static void I(string? tag, string? message)
    {
        Log(LEVEL_INFO, LogTag.N(tag), message, null);
    }

    public static void I(LogTag? tag, string? message)
    {
        Log(LEVEL_INFO, tag, message, null);
    }

    public static void W(string? tag, string? message)
    {
        Log(LEVEL_WARN, LogTag.N(tag), message, null);
    }

    public static void W(LogTag? tag, string? message)
    {
        Log(LEVEL_WARN, tag, message, null);
    }

    public static void E(string? tag, string? message)
    {
        Log(LEVEL_ERROR, LogTag.N(tag), message, null);
    }

    public static void E(LogTag? tag, string? message)
    {
        Log(LEVEL_ERROR, tag, message, null);
    }

    public static void E(string? tag, Exception? exception)
    {
        Log(LEVEL_ERROR, LogTag.N(tag), null, exception);
    }

    public static void E(LogTag? tag, Exception? exception)
    {
        Log(LEVEL_ERROR, tag, null, exception);
    }

    public static void E(string? tag, string? message, Exception? exception)
    {
        Log(LEVEL_ERROR, LogTag.N(tag), message, exception);
    }

    public static void E(LogTag? tag, string? message, Exception? exception)
    {
        Log(LEVEL_ERROR, tag, message, exception);
    }

    public static void F(string? tag, string? message)
    {
        AssertException exceptionNotNull = new(null, message);
        Log(LEVEL_FATAL, LogTag.N(tag), message, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void F(LogTag? tag, string? message)
    {
        AssertException exceptionNotNull = new(null, message);
        Log(LEVEL_FATAL, tag, message, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void F(string? tag, Exception? exception)
    {
        AssertException exceptionNotNull = new(null, exception);
        Log(LEVEL_FATAL, LogTag.N(tag), null, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void F(LogTag? tag, Exception? exception)
    {
        AssertException exceptionNotNull = new(null, exception);
        Log(LEVEL_FATAL, tag, null, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void F(string? tag, string? message, Exception? exception)
    {
        AssertException exceptionNotNull = new(null, message, exception);
        Log(LEVEL_FATAL, LogTag.N(tag), message, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void F(LogTag? tag, string? message, Exception? exception)
    {
        AssertException exceptionNotNull = new(null, message, exception);
        Log(LEVEL_FATAL, tag, message, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    public static void Assert(bool condition, string? eventName)
    {
        if (condition)
        {
            return;
        }

        AssertNotReachHereInternal(eventName, null, null);
    }

    public static void AssertNotReachHere(string? eventName)
    {
        AssertNotReachHereInternal(eventName, null, null);
    }

    public static void AssertNotReachHere(string? eventName, Exception? exception)
    {
        AssertNotReachHereInternal(eventName, null, exception);
    }

    public static void AssertNotReachHere(string? eventName, string? message, Exception? exception)
    {
        AssertNotReachHereInternal(eventName, message, exception);
    }

    private static void AssertNotReachHereInternal(string? eventName, string? message, Exception? exception)
    {
        AssertException exceptionNotNull = new(eventName, message, exception);
        Log(LEVEL_FATAL, LogTag.N("Assert", eventName), message, exceptionNotNull);
        FailOnDebug(exceptionNotNull);
    }

    private static void Console(string message)
    {
        Log(LEVEL_CONSOLE, LogTag.N(TAG), message, null);
    }

    private static void LogThreadMain()
    {
        Console("Logger initialized");

        while (true)
        {
            FlushToFile();
            Thread.Sleep(LOG_INTERVAL);
        }
    }

    private static void Log(int level, LogTag? tag, string? message, Exception? exception)
    {
        if (!Initialized)
        {
            return;
        }

        tag ??= LogTag.Empty;
        string levelTag;
        switch (level)
        {
            case LEVEL_CONSOLE:
                levelTag = "C";
                break;
            case LEVEL_DEBUG:
                levelTag = "D";
                break;
            case LEVEL_INFO:
                levelTag = "I";
                break;
            case LEVEL_WARN:
                levelTag = "W";
                break;
            case LEVEL_ERROR:
                levelTag = "E";
                break;
            case LEVEL_FATAL:
                levelTag = "F";
                break;
            default:
                FailOnDebug(new AssertException("CC13A1E9D13CC9EC"));
                levelTag = "U";
                break;
        }

        string realMessage = $"{DateTimeOffset.Now:yyyy/M/d HH:mm:ss.fff} [{levelTag},{tag}] {message}";
        if (exception != null)
        {
            realMessage += "\n" + exception.ToString();
        }

        if (DebugSwitchModel.Instance.ConsoleEnabled)
        {
            LogTag? consoleWhitelist = DebugSwitchModel.Instance.ConsoleWhitelist;
            if (consoleWhitelist == null || consoleWhitelist.ContainsAny(tag))
            {
                LogToConsole(realMessage);
            }
        }

        if (DebugUtils.DebugMode && level >= LEVEL_INFO)
        {
            var item = new LogItem
            {
                Tag = tag,
                Message = realMessage,
            };

            LogToFile(item);
        }

        foreach (ILogListener listener in sListeners)
        {
            listener.OnLog(level, tag, realMessage);
        }
    }

    private static void LogToFile(LogItem message)
    {
        sBuffer.Enqueue(message);
    }

    private static void FlushToFile()
    {
        if (sBuffer.IsEmpty)
        {
            return;
        }

        List<LogItem> logs = [];

        while (true)
        {
            if (sBuffer.TryDequeue(out LogItem? item))
            {
                logs.Add(item);
            }
            else
            {
                break;
            }
        }

        FlushToLogFile(logs);

        if (DebugSwitchModel.Instance.LogTreeEnabled)
        {
            FlushToLogTree(logs);
        }
    }

    private static void FlushToLogFile(List<LogItem> logs)
    {
        StringBuilder sb = new();
        foreach (LogItem item in logs)
        {
            sb.Append(item.Message);
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
        catch (Exception e)
        {
            F(TAG, e.ToString());
            return;
        }
    }

    private static void FlushToLogTree(List<LogItem> logs)
    {
        Dictionary<string, List<LogItem>> fileLogs = [];
        foreach (LogItem item in logs)
        {
            string[] tags = item.Tag.ToString().Split(',');
            foreach (string tag in tags)
            {
                string[] categories = tag.Split('/');
                bool divider = false;
                StringBuilder sb = new();
                foreach (string category in categories)
                {
                    if (divider)
                    {
                        sb.Append('\\');
                    }
                    divider = true;
                    sb.Append(category);
                    string path = sb.ToString();
                    if (!fileLogs.TryGetValue(path, out List<LogItem>? logItems))
                    {
                        logItems = [];
                        fileLogs[path] = logItems;
                    }
                    logItems.Add(item);
                }
            }
        }

        string cacheFolder = sLogFolderPath + "tree\\";
        string fileName = "log_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";

        foreach (KeyValuePair<string, List<LogItem>> pair in fileLogs)
        {
            StringBuilder sb = new();
            foreach (LogItem item in pair.Value)
            {
                sb.Append(item.Message);
                sb.Append('\n');
            }
            string content = sb.ToString();

            string folderPath = cacheFolder + pair.Key;
            string filePath = $"{folderPath}\\{fileName}";
            Directory.CreateDirectory(folderPath);
            using StreamWriter writer = new(filePath, true, Encoding.UTF8);
            writer.Write(content);
        }
    }

    private static void LogToConsole(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }

    private static void FailOnDebug(AssertException exception)
    {
        if (!Initialized)
        {
            return;
        }

        long time = GetTick();
        if (time - sLastErrorReportTime > 5000)
        {
            sLastErrorReportTime = time;
            SentryManager.CaptureWarning(exception);
        }

        if (DebugUtils.DebugMode)
        {
            CrashHandler.OnUnhandledException(exception);
        }
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }

    private class LogItem
    {
        public required LogTag Tag;
        public required string Message;
    }

    private class AssertException : Exception
    {
        private const string UNTITLED_EVENT = "UntitledEvent";

        public AssertException(string? eventName) : base(CombineMessage(eventName, null)) { }

        public AssertException(string? eventName, string? message) : base(CombineMessage(eventName, message)) { }

        public AssertException(string? eventName, Exception? inner) : base(CombineMessage(eventName, null), inner) { }

        public AssertException(string? eventName, string? message, Exception? inner) : base(CombineMessage(eventName, message), inner) { }

        private static string CombineMessage(string? eventName, string? message)
        {
            StringBuilder sb = new();
            if (string.IsNullOrEmpty(eventName))
            {
                sb.Append(UNTITLED_EVENT);
            }
            else
            {
                sb.Append(eventName);
            }

            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(" (").Append(message).Append(')');
            }

            return sb.ToString();
        }
    }

    public interface ILogListener
    {
        void OnLog(int level, LogTag tag, string message);
    }
}
