using System;
using System.Threading.Tasks;
using System.Windows;
using PostExporter.Utils;

namespace PostExporter;

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
            SentryUtil.HandleFeedbackForException((Exception)e.ExceptionObject);
        };

        DispatcherUnhandledException += (_, e) =>
        {
            SentryUtil.HandleFeedbackForException(e.Exception);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            SentryUtil.HandleFeedbackForException(e.Exception);
            e.SetObserved();
        };
    }
}