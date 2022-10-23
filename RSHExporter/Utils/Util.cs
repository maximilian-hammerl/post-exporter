using System;
using System.Diagnostics;
using System.Reflection;

namespace RSHExporter.Utils;

public static class Util
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

    public static string GetVersion(int fieldCount = 2)
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(fieldCount) ?? string.Empty;
    }

    public static string GetAppName()
    {
        return Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
    }

    public static string CapitalizeFirstChar(string text)
    {
        return string.Concat(text[0].ToString().ToUpper(), text.AsSpan(1));
    }
}