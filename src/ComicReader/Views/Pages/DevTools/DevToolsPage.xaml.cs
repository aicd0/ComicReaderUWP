// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.DevTools;

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

    private void CrashAppButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        throw new InvalidOperationException();
    }

    private void TriggerAssertFailureButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Logger.AssertNotReachHere("MockAssertFailure");
    }

    private void OnCommonConfigsApplyClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string configs = TbCommonConfigs.Text;
        try
        {
            DebugSwitchModel.Instance.SaveConfigFromJson(configs);
        }
        catch (Exception ex)
        {
            SetResult(ex.ToString());
            return;
        }

        SetResult("Successfully applied");
        RestoreConfig();
    }

    private void OnCommonConfigsRestoreClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RestoreConfig();
    }

    private void OnSyncFileNameClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
        {
            List<long> ids = [];
            await ComicData.Enqueue("SyncFileName", delegate
            {
                var command = SelectCommand.Create(ComicTable.Instance);
                command.AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false)));
                IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
                using SelectCommand.IReader reader = command.Execute();
                while (reader.Read())
                {
                    ids.Add(idToken.GetValue());
                }

                return true;
            });

            List<ComicModel> failedComics = [];
            List<Tuple<ComicModel, string>> comicToNewPath = [];
            foreach (long id in ids)
            {
                ComicModel? comic = await ComicModel.FromId(id, "SyncFileName");
                if (comic is null)
                {
                    continue;
                }

                string path = comic.Location;
                string parentDir = Path.GetDirectoryName(path) ?? string.Empty;
                if (string.IsNullOrEmpty(parentDir))
                {
                    failedComics.Add(comic);
                    continue;
                }

                string? newName = SanitizeForNtfsFileName(comic.Title);
                if (newName == null)
                {
                    failedComics.Add(comic);
                    continue;
                }

                string newPath;
                if (Directory.Exists(path))
                {
                    newPath = Path.Combine(parentDir, newName);
                }
                else
                {
                    failedComics.Add(comic);
                    continue;
                }

                if (path == newPath)
                {
                    continue;
                }

                comicToNewPath.Add(new(comic, newPath));
            }

            if (failedComics.Count > 0)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("Failed to process the following comics:");
                foreach (ComicModel comic in failedComics)
                {
                    stringBuilder.AppendLine($"  {comic.Location}");
                }

                DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
                    .SetTitle("Warning")
                    .SetContent(stringBuilder.ToString())
                    .SetPrimaryButtonText("OK")
                    .Build();
                await DialogUtils.EnqueueDialogAsync(options);
            }

            if (comicToNewPath.Count > 0)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("The following comics will be renamed:");
                foreach (Tuple<ComicModel, string> tuple in comicToNewPath)
                {
                    ComicModel comic = tuple.Item1;
                    string newPath = tuple.Item2;
                    stringBuilder.AppendLine($"  {comic.Location}  =>  {newPath}");
                }

                DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
                    .SetTitle("Warning")
                    .SetContent(stringBuilder.ToString())
                    .SetPrimaryButtonText("OK")
                    .SetSecondaryButtonText("Cancel")
                    .Build();
                if (await DialogUtils.EnqueueDialogAsync(options) != ContentDialogResult.Primary)
                {
                    SetResult("SyncFileName: Cancelled");
                    return;
                }

                foreach (Tuple<ComicModel, string> tuple in comicToNewPath)
                {
                    ComicModel comic = tuple.Item1;
                    string newPath = tuple.Item2;
                    await comic.MoveToLocation(newPath);
                }
            }

            SetResult("SyncFileName: Completed");
        });
    }

    private void OnShowDialogOnActiveWindowClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CoroutineUtils.Start(async () =>
        {
            await Task.Delay(3000);
            DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
                .SetContent("This is a test dialog")
                .SetPrimaryButtonText("Primary")
                .SetSecondaryButtonText("Secondary")
                .Build();
            ContentDialogResult result = await DialogUtils.EnqueueDialogAsync(options);
            SetResult($"Show dialog result: {result}");
        });
    }

    private void OnPrintMemoryLeakReportClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SetResult(MemoryLeakTracker.GenerateReport());
    }

    private void DeveloperModeToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugUtils.DeveloperMode = DeveloperModeToggleSwitch.IsOn;
    }

    private void SentryToggleSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DebugUtils.SentryEnabled = SentryToggleSwitch.IsOn;
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
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
        TbCommonConfigs.Text = DebugSwitchModel.Instance.GetConfigAsJson();
        DeveloperModeToggleSwitch.IsOn = DebugUtils.DeveloperMode;
        SentryToggleSwitch.IsOn = DebugUtils.SentryEnabled;
    }

    private static string? SanitizeForNtfsFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // NTFS invalid characters: \ / : * ? " < > | and control chars (0-31)
        string invalidChars = new(Path.GetInvalidFileNameChars());
        string pattern = $"[{Regex.Escape(invalidChars)}]";

        // Replace invalid characters with '_'
        string sanitized = Regex.Replace(input, pattern, "_");

        // Remove trailing dots and spaces (NTFS does not allow these)
        sanitized = sanitized.TrimEnd('.', ' ');

        // NTFS max file name length is 255 characters
        if (sanitized.Length > 255)
        {
            return null;
        }

        // If result is empty, return a default name
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }
}
