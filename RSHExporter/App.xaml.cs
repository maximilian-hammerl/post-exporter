using System;
using System.Threading.Tasks;
using System.Windows;
using RSHExporter.Utils;
using Sentry;

namespace RSHExporter;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupExceptionHandling();
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            FeedbackUtil.HandleFeedbackForException((Exception)e.ExceptionObject);
        };

        DispatcherUnhandledException += (_, e) =>
        {
            FeedbackUtil.HandleFeedbackForException(e.Exception);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            FeedbackUtil.HandleFeedbackForException(e.Exception);
            e.SetObserved();
        };

        SentrySdk.Init(o =>
        {
            o.Dsn = "https://57c119ee365d4707b7f99e2b36c8782d@o1430708.ingest.sentry.io/6781778";
            o.TracesSampleRate = 1.0;
            o.IsGlobalModeEnabled = true;
        });
    }
}