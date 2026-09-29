// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Archive;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.DevTools;

internal sealed partial class DevToolsPage : BasePage
{
    private const string TAG = nameof(DevToolsPage);
    private const int MAX_LOG_ITEM_COUNT = 10000;
    private const int COUNTER_UPDATE_INTERVAL = 500;

    public DevToolsPage()
    {
        InitializeComponent();

        _logListener = new LogListener(this);
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        GetMainPageAbility().SetTitle("Dev tools");
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Repair });

        StartOrStopLogger(AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_LOG_STARTED, true));
        UpdateLogToggleButton();

        InitializeCounters();
        UpdateCounters();
    }

    protected override void OnResume()
    {
        base.OnResume();

        SetResult(null);
        RestoreConfig();
    }

    protected override void OnStop()
    {
        base.OnStop();

        Logger.RemoveListener(_logListener);
    }

    //
    // Events
    //

    private void ApplyConfigsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string configs = CommonConfigsTextBlock.Text;
        try
        {
            DebugModel.SaveJsonConfig(configs);
        }
        catch (Exception ex)
        {
            SetResult(ex.ToString());
            return;
        }

        SetResult("Successfully applied");
        RestoreConfig();
    }

    private void RestoreConfigsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RestoreConfig();
    }

    private void ResetConfigsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugModel.SaveJsonConfig("null");
        RestoreConfig();
    }

    private void CrashAppButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        throw new InvalidOperationException("Test");
    }

    private void TriggerBackgroundTaskFailure_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TaskDispatcher.DefaultThreadPool.Submit(() =>
        {
            throw new InvalidOperationException("Test");
        });
    }

    private void TriggerAssertFailureButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Logger.AssertNotReachHere("MockAssertFailure");
    }

    private void ShowDialogOnActiveWindowButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(3000);
            DialogOptions options = new DialogOptions.Builder()
                .SetContent("This is a test dialog")
                .SetPrimaryButtonText("Primary")
                .SetSecondaryButtonText("Secondary")
                .Build();
            DialogResult result = await DialogUtils.EnqueueDialogAsync(options);
            SetResult($"Show dialog result: {result}");
        });
    }

    private void PrintMemoryLeakReportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetResult(MemoryLeakTracker.GenerateReport());
    }

    private void RunArchiveBenchmarkButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var button = (Button)sender;

        SetResult("Running archive benchmark...");
        button.IsEnabled = false;

        CoroutineUtils.Run(async () =>
        {
            string report;
            try
            {
                report = await TaskDispatcher.LongRunningThreadPool.Submit(RunArchiveBenchmark);
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
                report = ex.ToString();
            }

            await MainThreadUtils.RunInMainThread(() =>
            {
                button.IsEnabled = true;
                SetResult(report);
            });
        });
    }

    private void DeveloperModeToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugUtils.DeveloperMode = DeveloperModeToggleSwitch.IsOn;
    }

    //
    // Logs
    //

    private readonly LogListener _logListener;
    private bool _logStarted = false;
    private bool _logStickToLatest = true;

    public ObservableCollection<LogItemViewModel> LogItems { get; } = [];

    private void LogToggleButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        StartOrStopLogger(!_logStarted);
    }

    private void LogStickButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _logStickToLatest = LogStickButton.IsChecked == true;
        if (_logStickToLatest)
        {
            ScrollToLatestLog();
        }
    }

    private void StartOrStopLogger(bool started)
    {
        if (started && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (started == _logStarted)
        {
            return;
        }

        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_LOG_STARTED, started);
        _logStarted = started;
        if (started)
        {
            Logger.AddListener(_logListener);
        }

        UpdateLogToggleButton();
    }

    private void UpdateLogToggleButton()
    {
        LogToggleButton.Content = _logStarted ? "Pause" : "Start";
    }

    private void ScrollToLatestLog()
    {
        if (LogItems.Count == 0)
        {
            return;
        }

        LogListView.ScrollIntoView(LogItems[^1]);
    }

    private void AppendLog(string message)
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            LogItemViewModel item = new()
            {
                Text = message,
            };

            LogItems.Add(item);
            while (LogItems.Count > MAX_LOG_ITEM_COUNT)
            {
                LogItems.RemoveAt(0);
            }

            if (_logStickToLatest)
            {
                LogListView.ScrollIntoView(item);
            }
        });
    }

    private class LogListener(DevToolsPage page) : Logger.ILogListener
    {
        public void OnLog(Logger.LogItem item)
        {
            Interlocked.Increment(ref page._receivedLogCount);
            page.RequestCounterUpdate();

            if (!page._logStarted)
            {
                return;
            }

            if (item.Level <= 4)
            {
                List<LogTag?> consoleWhitelist = DebugModel.ConsoleWhitelist;
                if (!consoleWhitelist.Any(t => t is null || t.ContainsAny(item.Tag)))
                {
                    return;
                }
            }

            page.AppendLog(item.DisplayMessage);
        }
    }

    //
    // Counters
    //

    private readonly List<EventCounter> _counters = [];
    private long _receivedLogCount = 0L;
    private long _lastCounterUpdateTime = 0L;
    private int _counterUpdateScheduled = 0;

    private void InitializeCounters()
    {
        GlobalEvent events = GlobalEvent.Instance;
        _counters.Add(new EventCounter("Logs", () => Interlocked.Read(ref _receivedLogCount)));
        ObserveEventCounter("CollectionUpdated", events.CollectionUpdated);
        ObserveEventCounter("ComicUpdated", events.ComicUpdated);
        ObserveEventCounter("FilterUpdated", events.FilterUpdated);
        ObserveEventCounter("FavoriteUpdated", events.FavoriteUpdated);
        ObserveEventCounter("HistoryUpdated", events.HistoryUpdated);
        ObserveEventCounter("TagInfoUpdated", events.TagInfoUpdated);
    }

    private void ObserveEventCounter<T>(string name, ILiveDataObserveAbility<IValueObserver<T>> liveData) where T : notnull
    {
        long count = 0L;
        _counters.Add(new EventCounter(name, () => count));
        liveData.Observe(this, _ =>
        {
            count++;
            RequestCounterUpdate();
        });
    }

    private void RequestCounterUpdate()
    {
        long now = Environment.TickCount64;
        long last = Interlocked.Read(ref _lastCounterUpdateTime);
        if (now - last >= COUNTER_UPDATE_INTERVAL && Interlocked.CompareExchange(ref _lastCounterUpdateTime, now, last) == last)
        {
            CoroutineUtils.RunInMainThread(UpdateCounters);
            return;
        }

        if (Interlocked.CompareExchange(ref _counterUpdateScheduled, 1, 0) != 0)
        {
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            int remaining = (int)(COUNTER_UPDATE_INTERVAL - (Environment.TickCount64 - Interlocked.Read(ref _lastCounterUpdateTime)));
            if (remaining > 0)
            {
                await Task.Delay(remaining);
            }

            Interlocked.Exchange(ref _counterUpdateScheduled, 0);
            Interlocked.Exchange(ref _lastCounterUpdateTime, Environment.TickCount64);
            CoroutineUtils.RunInMainThread(UpdateCounters);
        });
    }

    private void UpdateCounters()
    {
        var builder = new StringBuilder();
        foreach (EventCounter counter in _counters)
        {
            builder.Append((counter.Name + ": ").PadRight(20)).Append(counter.Count.ToString("N0", CultureInfo.InvariantCulture)).AppendLine();
        }

        CounterTextBlock.Text = builder.ToString().TrimEnd();
    }

    private class EventCounter(string name, Func<long> getCount)
    {
        public string Name { get; } = name;

        public long Count => getCount();
    }

    //
    // Archive benchmark
    //

    private static string RunArchiveBenchmark()
    {
        const int archiveBenchmarkIterationCount = 10;
        string[] archiveBenchmarkPaths = [
            //@"D:\Documents\Comics\[赤坂アカ] かぐや様は告らせたい～天才たちの恋愛頭脳戦～ 第01-15巻\ZipTest.zip",
            @"D:\Documents\Comics\[赤坂アカ] かぐや様は告らせたい～天才たちの恋愛頭脳戦～ 第01-15巻\7ZTest.7z",
        ];

        var report = new StringBuilder();
        report.Append("Iterations: ").Append(archiveBenchmarkIterationCount).AppendLine();

        foreach (string path in archiveBenchmarkPaths)
        {
            report.AppendLine();
            report.Append("File: ").AppendLine(path);

            if (!File.Exists(path))
            {
                report.AppendLine("  [Skip] File not found");
                continue;
            }

            List<string> fileEntries = [];
            int totalEntries = 0;

            // Pre-scan to collect all entries.
            ArchiveManager.VisitEntries(path, entry =>
            {
                ++totalEntries;
                if (!entry.IsDirectory)
                {
                    fileEntries.Add(entry.FullName.Replace('/', '\\'));
                }
                return ArchiveManager.ICallbackResult.Continue;
            });

            report.Append("  Entries: ").Append(totalEntries)
                  .Append(", files: ").Append(fileEntries.Count).AppendLine();

            // 1) Time for iterating all entries.
            List<double> iterateTimes = [];
            for (int i = 0; i < archiveBenchmarkIterationCount; ++i)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                ArchiveManager.VisitEntries(path, entry =>
                {
                    using Stream stream = entry.Open();
                    return ArchiveManager.ICallbackResult.Continue;
                });
                stopwatch.Stop();
                iterateTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            AppendTiming(report, "Iterate (TryReadEntries)", iterateTimes);

            // 2) Time for opening all entries.
            List<double> openTimes = [];
            int openFailures = 0;
            for (int i = 0; i < archiveBenchmarkIterationCount; ++i)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                foreach (string entryName in fileEntries)
                {
                    using Stream? entryStream = ArchiveManager.OpenEntry(path, entryName);
                    if (entryStream is null)
                    {
                        ++openFailures;
                    }
                }
                stopwatch.Stop();
                openTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            AppendTiming(report, "Open all (TryGetFileStream)", openTimes);
            report.Append("  Open failures: ").AppendLine(openFailures.ToString());
        }

        return report.ToString();
    }

    private static void AppendTiming(StringBuilder report, string label, List<double> times)
    {
        if (times.Count == 0)
        {
            return;
        }

        double total = 0;
        double max = 0;
        foreach (double time in times)
        {
            total += time;
            if (time > max)
            {
                max = time;
            }
        }

        double average = total / times.Count;
        report.Append("  ").Append(label)
              .Append(": avg ").Append(average.ToString("0.###")).Append(" ms")
              .Append(", max ").Append(max.ToString("0.###")).AppendLine(" ms");
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }

    private void SetResult(string? result)
    {
        if (result == null || result.Length == 0)
        {
            result = "Operation result shows here";
        }

        result = $"{DateTimeOffset.Now:yyyy/M/d HH:mm:ss.fff}\n{result.TrimEnd()}";
        TbOperationResult.Text = result;
    }

    private void RestoreConfig()
    {
        CommonConfigsTextBlock.Text = DebugModel.LoadJsonConfig();
        DeveloperModeToggleSwitch.IsOn = DebugUtils.DeveloperMode;
    }
}
