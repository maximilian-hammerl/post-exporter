using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Scrape;
using RSHExporter.Utils;

namespace RSHExporter.View.Pages;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();

        VersionTextBlock.Text = Util.GetVersion();

        UsernameTextBox.Focus();
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleLoginButtonLoading(true);

        var username = UsernameTextBox.Text;
        if (string.IsNullOrEmpty(username))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.LoginMissingUsername);
            return;
        }

        var password = PasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.LoginMissingPassword);
            return;
        }

        var groups = await Scraper.LoginAndGetGroups(username, password);
        if (groups == null)
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.LoginWrongUsernamePassword);
            return;
        }

        if (groups.Count == 0)
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowError(RSHExporter.Resources.Localization.Resources.LoginNoGroups);
            return;
        }

        ToggleLoginButtonLoading(false);
        NavigationService.Navigate(new SelectPage(groups));
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new WelcomePage());
    }

    private void ToggleLoginButtonLoading(bool isLoading)
    {
        LoginButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogUtil.ShowHelpAndHighlight(
            (brush =>
                {
                    UsernameTextBox.Background = brush;
                    PasswordBox.Background = brush;
                },
                RSHExporter.Resources.Localization.Resources.HelpLoginStep1),
            (brush => LoginButtonContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpLoginStep2)
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        FeedbackUtil.HandleFeedback("LoginPage");
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}