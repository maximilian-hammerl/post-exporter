using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Scrape;
using RSHExporter.Utils;
using Sentry;

namespace RSHExporter.View.Pages;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();

        VersionTextBlock.Text = $"Version {Util.GetVersion()}";

        UsernameTextBox.Focus();

        SentryUtil.HandleBreadcrumb(
            "Opened page",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleLoginButtonLoading(true);

        var username = UsernameTextBox.Text;
        if (string.IsNullOrEmpty(username))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningMissingUsername);
            return;
        }

        var password = PasswordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningMissingPassword);
            return;
        }

        SentryUtil.HandleBreadcrumb(
            "Trying to login",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );

        (List<Group>? groups, bool loadedAllGroupsSuccessfully) response;
        try
        {
            response = await Scraper.LoginAndGetGroups(username, password);
        }
        catch (HttpRequestException exception)
        {
            SentryUtil.HandleBreadcrumb(
                "Could not login because of server problems",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );
            SentryUtil.HandleException(exception);

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowError(RSHExporter.Resources.Localization.Resources.ErrorServerUnavailable, false);
            return;
        }

        if (response.groups == null)
        {
            SentryUtil.HandleBreadcrumb(
                "Wrong username or password",
                "LoginPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningWrongUsernamePassword);
            return;
        }

        if (!response.loadedAllGroupsSuccessfully)
        {
            SentryUtil.HandleBreadcrumb(
                "Not all groups loaded successfully",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );

            DialogUtil.ShowError(RSHExporter.Resources.Localization.Resources.ErrorSomeGroupsFailedToLoad, true);
        }

        if (response.groups.Count == 0)
        {
            SentryUtil.HandleBreadcrumb(
                "No groups could be loaded",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowError(RSHExporter.Resources.Localization.Resources.ErrorNoGroups, false);
            return;
        }

        SentryUtil.UpdateUser(username);

        SentryUtil.HandleBreadcrumb(
            "Successful logged in",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );

        ToggleLoginButtonLoading(false);
        NavigationService.Navigate(new SelectPage(response.groups));
    }

    private void ToggleLoginButtonLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        DefaultIcon.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingIcon.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Opened help",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );

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
        SentryUtil.HandleFeedback("LoginPage");
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}