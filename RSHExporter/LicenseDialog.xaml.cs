using System.Windows;

namespace RSHExporter;

public partial class LicenseDialog : Window
{
    public LicenseDialog()
    {
        InitializeComponent();
    }

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}