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

    private async void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!SentryUtil.CollectDataAccepted)
        {
            SentryUtil.CollectDataAccepted =
                DialogUtil.ShowQuestion(PostExporter.Resources.Localization.Resources.WelcomeAllowSentryQuestion);
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
        SentryUtil.CollectDataAccepted = true;
    }

    private void CollectDataCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        SentryUtil.CollectDataAccepted = false;
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}