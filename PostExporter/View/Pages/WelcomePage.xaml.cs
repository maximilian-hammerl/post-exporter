using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using PostExporter.Utils;
using Sentry;

namespace PostExporter.View.Pages;

public partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();

        CollectDataCheckBox.IsChecked = ApplicationConfiguration.CollectDataAccepted;

        VersionTextBlock.Text = $"Version {Util.GetCurrentVersionAsString()}";
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
        SentryUtil.HandleFeedback("FeedbackPage");
    }

    private async void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ApplicationConfiguration.CollectDataAccepted)
        {
            ApplicationConfiguration.CollectDataAccepted =
                DialogUtil.ShowQuestion(PostExporter.Resources.Localization.Resources.WelcomeAllowSentryQuestion);
        }

        if (ApplicationConfiguration.CollectDataAccepted)
        {
            SentryUtil.InitializeSentry();
        }

        await CheckCurrentVersion();
    }

    private async Task CheckCurrentVersion()
    {
        var latestRelease = await GitHubUtil.GetLatestRelease();
        var latestVersion = latestRelease.GetVersion();

        var currentVersion = Util.GetCurrentVersion();

        if (currentVersion.Major <= latestVersion.Major && currentVersion.Minor < latestVersion.Minor)
        {
            var newReleaseDialog = new NewReleaseDialog(latestRelease, currentVersion);
            newReleaseDialog.ShowDialog();

            if (newReleaseDialog.DialogResult is true)
            {
                Window.GetWindow(this).Close();
                return;
            }
        }

        NavigationService.Navigate(new LoginPage());
    }

    private void CollectDataCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ApplicationConfiguration.CollectDataAccepted = true;
    }

    private void CollectDataCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ApplicationConfiguration.CollectDataAccepted = false;
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}