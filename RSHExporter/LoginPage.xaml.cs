using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Scrape;
using RSHExporter.Utils;
using Sentry;

namespace RSHExporter;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}";
        }

        var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
        if (currentCulture == "de" || currentCulture.StartsWith("de-"))
        {
            ToGermanButton.IsEnabled = false;
        }
        else if (currentCulture == "en" || currentCulture.StartsWith("en-"))
        {
            ToEnglishButton.IsEnabled = false;
        }

        Username.Focus();
    }

    private void ToGermanButton_OnClick(object sender, RoutedEventArgs e)
    {
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de");
        NavigationService.Navigate(new LoginPage());
    }

    private void ToEnglishButton_OnClick(object sender, RoutedEventArgs e)
    {
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
        NavigationService.Navigate(new LoginPage());
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleLoginButtonLoading(true);

        var username = Username.Text;
        if (string.IsNullOrEmpty(username))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.LoginMissingUsername);
            return;
        }

        var password = Password.Password;
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

        SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new User
            {
                Username = username,
            };
        });

        ToggleLoginButtonLoading(false);
        NavigationService.Navigate(new SelectPage(groups));
    }

    private void ToggleLoginButtonLoading(bool isLoading)
    {
        LoginButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogUtil.ShowHelpAndHighlight(
            (brush => UsernamePasswordContent.Background = brush,
                "First enter the username and password you use on Rollenspielhimmel here."),
            (brush => LoginContent.Background = brush,
                "Then click here to login and continue with selecting your groups and threads.")
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        FeedbackUtil.HandleFeedback("LoginPage");
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true
        };
        process.Start();

        e.Handled = true;
    }
}