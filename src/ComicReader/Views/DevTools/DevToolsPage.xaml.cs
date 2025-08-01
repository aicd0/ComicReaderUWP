// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Legacy;
using ComicReader.Data;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.Views.Main;

using Microsoft.UI.Xaml.Controls;

using Windows.Storage;
using Windows.System;

namespace ComicReader.Views.DevTools;

internal sealed partial class DevToolsPage : BasePage
{
    private const string TAG = nameof(DevToolsPage);

    public DevToolsPage()
    {
        InitializeComponent();
    }

    //
    // Page Lifecycle
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

    private void OnOpenAppFolderClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        C0.Run(async () =>
        {
            string path = StorageLocation.GetLocalFolderPath();
            StorageFolder? folder = await Storage.TryGetFolder(path);
            if (folder != null)
            {
                _ = Launcher.LaunchFolderAsync(folder);
            }
        });
    }

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
            DebugUtils.SaveConfigFromJson(configs);
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
        C0.Run(async () =>
        {
            List<long> ids = [];
            await ComicData.EnqueueCommand(delegate
            {
                var command = new SelectCommand(ComicTable.Instance);
                command.AppendCondition(new ComparisonCondition(ColumnOrValue.FromColumn(ComicTable.ColumnHidden), ColumnOrValue.FromValue(false)));
                IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
                using SelectCommand.IReader reader = command.Execute(SqlDatabaseManager.MainDatabase);
                while (reader.Read())
                {
                    ids.Add(idToken.GetValue());
                }
            }, "SyncFileName");

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
                    continue;
                }

                string? newName = SanitizeForNtfsFileName(comic.Title);
                if (newName == null)
                {
                    continue;
                }

                string newPath;
                if (File.Exists(path))
                {
                    string extension = Path.GetExtension(path);
                    newPath = Path.Combine(parentDir, $"{newName}{extension}");
                }
                else if (Directory.Exists(path))
                {
                    newPath = Path.Combine(parentDir, newName);
                }
                else
                {
                    continue;
                }

                if (path == newPath)
                {
                    continue;
                }

                await comic.MoveToLocation(newPath);
            }
        });
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

        TbOperationResult.Text = result;
    }

    private void RestoreConfig()
    {
        TbCommonConfigs.Text = DebugUtils.GetConfigAsJson();
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
