using System;
using System.Diagnostics;
using System.Reflection;

namespace PostExporter.Utils;

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

    public static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ??
               throw new InvalidOperationException("Cannot get current version");
    }

    public static string GetCurrentVersionAsString(int fieldCount = 2)
    {
        return GetCurrentVersion().ToString(fieldCount);
    }

    public static string GetAppName()
    {
        return Assembly.GetExecutingAssembly().GetName().Name ??
               throw new InvalidOperationException("Cannot get app name");
    }

    public static string CapitalizeFirstChar(string text)
    {
        return string.Concat(text[0].ToString().ToUpper(), text.AsSpan(1));
    }
}