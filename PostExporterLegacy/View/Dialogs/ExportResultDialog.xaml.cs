using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using PostExporter.Export;
using PostExporter.Scrape;
using PostExporter.Utils;
using Sentry;

namespace PostExporter.View.Dialogs;

public partial class ExportResultDialog : Window
{
    public ExportResultDialog(ReadOnlyCollection<Thread> threads,
        ConcurrentDictionary<ExportError, ConcurrentBag<Thread>> failedExports, bool wasCancelled)
    {
        InitializeComponent();

        ExportResults = [];

        var successfulThreads = new List<Thread>(threads);

        foreach (var (exportError, failedThreads) in failedExports)
        {
            if (!failedThreads.IsEmpty)
            {
                SentryUtil.HandleBreadcrumb(
                    $"{failedThreads.Count} of {threads.Count} exports failed because of {exportError}",
                    "ExportResultDialog",
                    level: BreadcrumbLevel.Error
                );

                ExportResults.Add(new ExportResult(
                    exportError.ErrorMessage(),
                    "Solid_CircleExclamation",
                    "Red",
                    failedThreads.ToList()
                ));

                successfulThreads.RemoveAll(thread =>
                {
                    return failedThreads.Select(failedThread => failedThread.Id).Contains(thread.Id);
                });
            }
        }

        if (successfulThreads.Count > 0)
        {
            if (wasCancelled)
            {
                SentryUtil.HandleBreadcrumb(
                    $"Export of {successfulThreads.Count} of {threads.Count} threads was cancelled",
                    "ExportResultDialog",
                    level: BreadcrumbLevel.Warning
                );

                ExportResults.Add(new ExportResult(
                    PostExporter.Resources.Localization.Resources.InfoCancelledThreadsExported,
                    "Solid_Bolt",
                    "Orange",
                    successfulThreads
                ));
            }
            else
            {
                SentryUtil.HandleBreadcrumb(
                    $"Successfully exported {successfulThreads.Count} of {threads.Count} threads",
                    "ExportResultDialog",
                    level: BreadcrumbLevel.Info
                );

                ExportResults.Add(new ExportResult(
                    successfulThreads.Count == threads.Count
                        ? PostExporter.Resources.Localization.Resources.InfoAllThreadsSuccessfullyExported
                        : PostExporter.Resources.Localization.Resources.InfoThreadsSuccessfullyExported,
                    "Solid_Check",
                    "Green",
                    successfulThreads
                ));
            }
        }

        ExportResultItems.DataContext = ExportResults;

        if (!string.IsNullOrEmpty(ExportConfiguration.DirectoryPath))
        {
            Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
        }
    }

    [UsedImplicitly] public ObservableCollection<ExportResult> ExportResults { get; set; }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UrlButton_OnClick(object sender, RoutedEventArgs e)
    {
        Util.OpenBrowser((string)((Button)sender).Tag);
    }

    public class ExportResult
    {
        public ExportResult(string message, string icon, string color, List<Thread> threads)
        {
            Message = message;
            Icon = icon;
            Color = color;
            Threads = threads;
        }

        [UsedImplicitly] public string Message { get; }
        [UsedImplicitly] public string Icon { get; }
        [UsedImplicitly] public string Color { get; }
        [UsedImplicitly] public List<Thread> Threads { get; }
    }
}