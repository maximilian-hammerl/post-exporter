using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using PostExporter.Scrape;
using PostExporter.Utils;
using Sentry;

namespace PostExporter.View.Pages;

public partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();

        BaseUrlTextBox.Text = Scraper.BaseUrl ?? "rollenspielhimmel.de";

        VersionTextBlock.Text = $"Version {Util.GetCurrentVersionAsString()}";

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

        var baseUrl = BaseUrlTextBox.Text;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningMissingBaseUrl);
            return;
        }

        if (baseUrl.StartsWith("http://"))
        {
            baseUrl = baseUrl.Replace("http://", "https://");
        }
        else if (!baseUrl.StartsWith("https://"))
        {
            baseUrl = $"https://{baseUrl}";
        }

        var isValidUri = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uriResult) &&
                         uriResult.Scheme == Uri.UriSchemeHttps;

        if (!isValidUri || uriResult == null)
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningInvalidBaseUrl);
            return;
        }

        Scraper.BaseUrl = uriResult.GetLeftPart(UriPartial.Authority);

        var username = UsernameTextBox.Text;
        if (string.IsNullOrWhiteSpace(username))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningMissingUsername);
            return;
        }

        var password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningMissingPassword);
            return;
        }

        SentryUtil.HandleBreadcrumb(
            "Trying to login",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );

        bool successfulLogin;
        try
        {
            successfulLogin = await Scraper.Login(username, password);
        }
        catch (HttpRequestException exception)
        {
            SentryUtil.HandleBreadcrumb(
                $"Could not login to {baseUrl} ({Scraper.BaseUrl}) because of server problems",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );
            SentryUtil.HandleMessage($"{exception} for \"{baseUrl}\" (\"{Scraper.BaseUrl}\")");

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowError(PostExporter.Resources.Localization.Resources.ErrorServerUnavailable, false);
            return;
        }

        if (!successfulLogin)
        {
            SentryUtil.HandleBreadcrumb(
                "Wrong username or password",
                "LoginPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningWrongUsernamePassword);
            return;
        }

        (List<Group> groups, bool loadedAllGroupsSuccessfully) response = await Scraper.GetGroups();

        if (response.groups.Count == 0)
        {
            SentryUtil.HandleBreadcrumb(
                "No groups could be loaded",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );

            ToggleLoginButtonLoading(false);
            DialogUtil.ShowError(PostExporter.Resources.Localization.Resources.ErrorNoGroups, false);
            return;
        }

        if (!response.loadedAllGroupsSuccessfully)
        {
            SentryUtil.HandleBreadcrumb(
                "Not all groups loaded successfully",
                "LoginPage",
                level: BreadcrumbLevel.Error
            );

            DialogUtil.ShowError(PostExporter.Resources.Localization.Resources.ErrorSomeGroupsFailedToLoad, true);
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

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new WelcomePage());
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Opened help",
            "LoginPage",
            level: BreadcrumbLevel.Info
        );

        DialogUtil.ShowHelpAndHighlight(
            (brush => BaseUrlTextBox.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpLoginStep1),
            (brush =>
                {
                    UsernameTextBox.Background = brush;
                    PasswordBox.Background = brush;
                },
                PostExporter.Resources.Localization.Resources.HelpLoginStep2),
            (brush => LoginButtonContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpLoginStep3)
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