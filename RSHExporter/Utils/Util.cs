﻿using System;
using System.Diagnostics;
using System.Reflection;

namespace RSHExporter.Utils;

public class Util
{
    public static void OpenBrowser(Uri uri)
    {
        OpenBrowser(uri.AbsoluteUri);
    }

    public static void OpenBrowser(string uri)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(uri)
        {
            UseShellExecute = true
        };
        process.Start();
    }

    public static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"Version {version.Major}.{version.Minor}" : "";
    }
}