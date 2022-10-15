using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using RSHExporter.Utils;
using Sentry;

namespace RSHExporter;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false);

        var configuration = builder.Build();
        var sentrySection = configuration.GetRequiredSection("Sentry");
        var dns = sentrySection["DNS"];

        if (dns == null)
        {
            throw new ArgumentNullException(dns);
        }

        SetupExceptionHandling(dns);
    }

    private void SetupExceptionHandling(string dns)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            FeedbackUtil.HandleException((Exception)e.ExceptionObject);
        };

        DispatcherUnhandledException += (_, e) =>
        {
            FeedbackUtil.HandleException(e.Exception);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            FeedbackUtil.HandleException(e.Exception);
            e.SetObserved();
        };

        SentrySdk.Init(o =>
        {
            o.Dsn = dns;
            o.TracesSampleRate = 1.0;
        });
    }
}