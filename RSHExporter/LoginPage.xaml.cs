using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Scrape;
using RSHExporter.Utils;

namespace RSHExporter;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        var username = Username.Text;
        if (string.IsNullOrEmpty(username))
        {
            DialogUtil.ShowWarning("Please enter your username first!");
            return;
        }

        var password = Password.Password;
        if (string.IsNullOrEmpty(password))
        {
            DialogUtil.ShowWarning("Please enter your password first!");
            return;
        }

        var groups = await Scraper.LoginAndGetGroups(username, password);
        if (groups == null)
        {
            DialogUtil.ShowWarning("You could not be logged in! Please check your username and password.");
            return;
        }

        if (groups.Count == 0)
        {
            DialogUtil.ShowError("No groups could be loaded! Either you have no groups or something went wrong.");
            return;
        }

        NavigationService.Navigate(new SelectPage(groups));
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