// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReaderUWP.Core.Common.Constants;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Core.Common.DebugTools;

internal static class CrashHandler
{
    public static void OnUnhandledException(Exception exception)
    {
        IApplicationService? appService = ServiceManager.GetServiceNullable<IApplicationService>();

        StringBuilder sb = new();

        sb.Append("Message:\n");
        sb.Append(exception.Message);

        sb.Append("\n\n");
        sb.Append("Exception stack trace:\n");
        sb.Append(exception.ToString());

        sb.Append("\n\n");
        sb.Append("Caller stack trace:\n");
        sb.Append(new System.Diagnostics.StackTrace(true).ToString());

        sb.Append('\n');
        sb.Append("Environment information:\n");
        sb.Append("Crash time: ");
        sb.Append(DateTimeOffset.Now.ToString("yyyy/M/d HH:mm:ss.fff"));
        if (appService is not null)
        {
            sb.Append('\n');
            sb.Append(appService.GetEnvironmentDebugInfo());
        }

        string crashReport = sb.ToString();
        try
        {
            string fileName = $"crash_report_{DateTimeOffset.Now:yyyyMMddHHmmss}_{RandomString(4)}.txt";
            string filePath = StorageLocation.LocalCacheFolderPath + "\\" + fileName;
            using StreamWriter writer = new(filePath, true, Encoding.UTF8);
            writer.Write(crashReport);
            Logger.Flush();
            SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_CRASH_REPORT, crashReport);
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
        }

        if (DebugUtils.DeveloperMode && System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }

        if (DebugUtils.DebugMode)
        {
            ServiceManager.GetService<IDebugService>().OnCrashReport(crashReport);
        }
    }

    public static void ReportLastCrash()
    {
        string? crashReport = SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValue<string>(DatabaseEntry.KV_KEY_MAIN_CRASH_REPORT);
        if (string.IsNullOrEmpty(crashReport))
        {
            return;
        }

        SdkDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_CRASH_REPORT, string.Empty);
        ServiceManager.GetService<IDebugService>().OnCrashReport(crashReport);
    }

    private static string RandomString(int length)
    {
        const string symbols = "0123456789abcdefghijklmnopqrstuvwxyz";
        StringBuilder sb = new();
        for (int i = 0; i < length; ++i)
        {
            sb.Append(symbols[Random.Shared.Next(symbols.Length)]);
        }
        return sb.ToString();
    }

    private static void Console(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }
}
