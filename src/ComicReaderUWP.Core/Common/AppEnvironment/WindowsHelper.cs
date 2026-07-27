// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

// Licensed to the Microsoft Corporation under one or more agreements.
// The Microsoft Corporation licenses this file to you under the MIT license.

#nullable disable

using System.Drawing;
using System.Reflection;

using ComicReaderUWP.Core.Common.Native;

namespace ComicReaderUWP.Core.Common.AppEnvironment;

internal static class WindowsHelper
{
    private const long APPMODEL_ERROR_NO_PACKAGE = 15700L;

    private const uint EVENT_SYSTEM_MINIMIZESTART = 22u;

    private const uint EVENT_SYSTEM_MINIMIZEEND = 23u;

    private const uint WINEVENT_OUTOFCONTEXT = 0u;

    private static readonly IDictionary<NativeModels.WinEventDelegate, nint> _hooks;

    private const int DESKTOPVERTRES = 117;

    private const int DESKTOPHORZRES = 118;

    private static readonly int Minimized;

    public static bool IsRunningAsWpf { get; }

    public static bool IsRunningAsUwp { get; }

    public static bool IsRunningAsWinUI { get; }

    public static dynamic WpfApplication { get; }

    public static event NativeModels.WinEventDelegate OnMinimized
    {
        add
        {
            uint id = (uint)Environment.ProcessId;
            nint value2 = NativeMethods.SetWinEventHook(22u, 23u, nint.Zero, value, id, 0u, 0u);
            _hooks.Add(value, value2);
        }
        remove
        {
            if (_hooks.TryGetValue(value, out nint value2))
            {
                NativeMethods.UnhookWinEvent(value2);
            }
        }
    }

    private static bool EvalIsRunningAsUwp()
    {
        try
        {
            return Assembly.GetEntryAssembly().GetReferencedAssemblies().Any((referencedAssembly) => referencedAssembly.Name == "Windows.UI.Xaml");
        }
        catch (Exception)
        {
        }

        return false;
    }

    private static bool EvalIsRunningAsWinUI()
    {
        try
        {
            return Assembly.GetEntryAssembly().GetReferencedAssemblies().Any((referencedAssembly) => referencedAssembly.Name == "Microsoft.UI.Xaml" || referencedAssembly.Name == "Microsoft.WinUI");
        }
        catch (Exception)
        {
        }

        return false;
    }

    static WindowsHelper()
    {
        _hooks = new Dictionary<NativeModels.WinEventDelegate, nint>();
        try
        {
            Assembly assembly = GetAssembly("PresentationFramework");
            IsRunningAsWpf = assembly != null;
            if (IsRunningAsWpf)
            {
                Type type = assembly.GetType("System.Windows.Application");
                WpfApplication = type.GetRuntimeProperty("Current")?.GetValue(type);
                Minimized = (int)assembly.GetType("System.Windows.WindowState").GetField("Minimized").GetRawConstantValue();
            }
        }
        catch (AppDomainUnloadedException)
        {
        }

        IsRunningAsUwp = EvalIsRunningAsUwp();
        IsRunningAsWinUI = IsRunningAsUwp || EvalIsRunningAsWinUI();
    }

    private static Assembly GetAssembly(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((assembly) => assembly.GetName().Name == name);
    }

    private static Rectangle WindowsRectToRectangle(dynamic windowsRect)
    {
        var result = default(Rectangle);
        result.X = (int)windowsRect.X;
        result.Y = (int)windowsRect.Y;
        result.Width = (int)windowsRect.Width;
        result.Height = (int)windowsRect.Height;
        return result;
    }
}
