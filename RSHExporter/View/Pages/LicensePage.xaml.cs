using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Utils;

namespace RSHExporter.View.Pages;

public partial class LicensePage : Page
{
    public LicensePage()
    {
        InitializeComponent();

        VersionTextBlock.Text = $"Version {Util.GetCurrentVersionAsString()}";

        var currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
        if (currentCulture == "de" || currentCulture.StartsWith("de-"))
        {
            ToGermanButton.IsEnabled = false;
        }
        else if (currentCulture == "en" || currentCulture.StartsWith("en-"))
        {
            ToEnglishButton.IsEnabled = false;
        }
    }

    private void AcceptButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new WelcomePage());
    }

    private void DeclineButton_OnClick(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this).Close();
    }

    private void ToGermanButton_OnClick(object sender, RoutedEventArgs e)
    {
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("de");
        NavigationService.Navigate(new LicensePage());
    }

    private void ToEnglishButton_OnClick(object sender, RoutedEventArgs e)
    {
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
        NavigationService.Navigate(new LicensePage());
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}