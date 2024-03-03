using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using PostExporter.Utils;
using PostExporter.View.Dialogs;
using Sentry;

namespace PostExporter.View.Pages;

public partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();

        CollectDataCheckBox.IsChecked = SentryUtil.CollectDataAccepted;

        VersionTextBlock.Text = $"Version {Util.GetCurrentVersionAsString()}";
    }

    private async void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!SentryUtil.CollectDataAccepted)
        {
            SentryUtil.CollectDataAccepted =
                DialogUtil.ShowQuestion(PostExporter.Resources.Localization.Resources.WelcomeAllowSentryQuestion,
                    "Sentry");
        }

        await CheckCurrentVersion();
    }

    private async Task CheckCurrentVersion()
    {
        SentryUtil.HandleBreadcrumb(
            "Checking for current version",
            "WelcomePage",
            level: BreadcrumbLevel.Info
        );

        if (await CheckCloseForNewVersion())
        {
            SentryUtil.HandleBreadcrumb(
                "Closing Exporter",
                "WelcomePage",
                level: BreadcrumbLevel.Info
            );

            Window.GetWindow(this).Close();
        }
        else
        {
            SentryUtil.HandleBreadcrumb(
                "To Login Page",
                "WelcomePage",
                level: BreadcrumbLevel.Info
            );

            NavigationService.Navigate(new LoginPage());
        }
    }

    private static async Task<bool> CheckCloseForNewVersion()
    {
        var latestRelease = await GitHubUtil.GetLatestRelease();
        var latestVersion = latestRelease.GetVersion();

        var currentVersion = Util.GetCurrentVersion();

        SentryUtil.HandleBreadcrumb(
            $"Current version is {currentVersion.ToString()}, latest version is {latestVersion.ToString()}",
            "WelcomePage",
            level: BreadcrumbLevel.Info
        );

        if (currentVersion.Major <= latestVersion.Major && currentVersion.Minor < latestVersion.Minor)
        {
            SentryUtil.HandleBreadcrumb(
                "New release available",
                "WelcomePage",
                level: BreadcrumbLevel.Info
            );

            var newReleaseDialog = new NewReleaseDialog(latestRelease, currentVersion);
            newReleaseDialog.ShowDialog();

            if (newReleaseDialog.DialogResult is true)
            {
                SentryUtil.HandleBreadcrumb(
                    "Close Exporter because of new release",
                    "WelcomePage",
                    level: BreadcrumbLevel.Info
                );

                return true;
            }

            SentryUtil.HandleBreadcrumb(
                "Ignoring new release",
                "WelcomePage",
                level: BreadcrumbLevel.Info
            );
        }
        else
        {
            SentryUtil.HandleBreadcrumb(
                "Already on latest release",
                "WelcomePage",
                level: BreadcrumbLevel.Info
            );
        }

        SentryUtil.HandleBreadcrumb(
            "Continuing with current version",
            "WelcomePage",
            level: BreadcrumbLevel.Info
        );

        return false;
    }

    private void CollectDataCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        SentryUtil.CollectDataAccepted = true;
    }

    private void CollectDataCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        SentryUtil.CollectDataAccepted = false;
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new LicensePage());
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Opened help",
            "WelcomePage",
            level: BreadcrumbLevel.Info
        );

        DialogUtil.ShowHelpAndHighlight(
            (brush => { WelcomeTextBlock.Background = brush; },
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep1),
            (brush => CollectDataContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep2),
            (brush => RepositoryTextBlock.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep3),
            (brush => VersionTextBlock.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep4),
            (brush => TeamTextBlock.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep5),
            (brush => ContinueButtonContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpWelcomeStep6)
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleFeedback("WelcomePage");
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}