// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Storage;

namespace ComicReaderUWP.Core.Common.DebugTools;

internal static class CrashHandler
{
    public static void OnCrash(Exception exception)
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
        WriteCrashReport($"CrashReport_{DateTimeOffset.Now:yyyyMMddHHmmss}_{RandomString(4)}.txt", crashReport);
        WriteCrashReport("CrashReport.txt", crashReport);

        if (System.Diagnostics.Debugger.IsAttached && DebugUtils.DeveloperMode)
        {
            System.Diagnostics.Debugger.Break();
        }

        ServiceManager.GetService<IDebugService>().OnCrashReport(crashReport);
    }

    public static void ReportLastCrash()
    {
        string? crashReport = ReadAndDeleteCrashReport("CrashReport.txt");
        if (string.IsNullOrEmpty(crashReport))
        {
            return;
        }

        ServiceManager.GetService<IDebugService>().OnCrashReport(crashReport);
    }

    private static void WriteCrashReport(string fileName, string content)
    {
        string filePath = Path.Combine(StorageLocation.LocalCacheFolderPath, fileName);
        try
        {
            using StreamWriter writer = new(filePath, true, Encoding.UTF8);
            writer.Write(content);
            Logger.Flush();
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
        }
    }

    private static string? ReadAndDeleteCrashReport(string fileName)
    {
        string filePath = Path.Combine(StorageLocation.LocalCacheFolderPath, fileName);
        string content;
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs);
            content = sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
            return null;
        }

        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
        }

        return content;
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
