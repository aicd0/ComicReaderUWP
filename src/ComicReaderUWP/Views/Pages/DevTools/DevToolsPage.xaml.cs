// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Models;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.DevTools;

internal sealed partial class DevToolsPage : BasePage
{
    private const string TAG = nameof(DevToolsPage);

    public DevToolsPage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        GetMainPageAbility().SetTitle("Dev tools");
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Repair });
    }

    protected override void OnResume()
    {
        base.OnResume();

        SetResult(null);
        RestoreConfig();
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

            string extension = Path.GetExtension(path);
            List<string> fileEntries = [];
            int totalEntries = 0;

            // Pre-scan to collect all entries.
            using (Stream? scanStream = ArchiveAccess.TryGetFileStream(path))
            {
                if (scanStream is null)
                {
                    report.AppendLine("  [Skip] Unable to open the archive");
                    continue;
                }

                ArchiveAccess.TryReadEntries(scanStream, extension, entry =>
                {
                    ++totalEntries;
                    if (!entry.IsDirectory)
                    {
                        fileEntries.Add(entry.FullName.Replace('/', '\\'));
                    }
                    return ArchiveAccess.ICallbackResult.Continue;
                });
            }

            report.Append("  Entries: ").Append(totalEntries)
                  .Append(", files: ").Append(fileEntries.Count).AppendLine();

            // 1) Time for iterating all entries.
            List<double> iterateTimes = [];
            for (int i = 0; i < archiveBenchmarkIterationCount; ++i)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using (Stream? stream = ArchiveAccess.TryGetFileStream(path))
                {
                    if (stream is null)
                    {
                        break;
                    }

                    ArchiveAccess.TryReadEntries(stream, extension, entry =>
                    {
                        using Stream stream = entry.Open();
                        return ArchiveAccess.ICallbackResult.Continue;
                    });
                }
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
                    using Stream? entryStream = ArchiveAccess.TryGetFileStream(path, entryName);
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
