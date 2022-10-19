using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using RSHExporter.Utils;

namespace RSHExporter;

public partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();

        CollectDataCheckBox.IsChecked = CollectDataAccepted;

        VersionTextBlock.Text = Util.GetVersion();
    }

    public static bool CollectDataAccepted { get; private set; } = false;

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        throw new System.NotImplementedException();
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        throw new System.NotImplementedException();
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new LoginPage());
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new LicensePage());
    }

    private void CollectDataCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        CollectDataAccepted = true;
    }

    private void CollectDataCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        CollectDataAccepted = false;
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}