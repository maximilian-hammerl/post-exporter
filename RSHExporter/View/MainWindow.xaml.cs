using System.Windows;
using RSHExporter.View.Pages;

namespace RSHExporter.View;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MainFrame.Navigate(new LicensePage());
    }
}