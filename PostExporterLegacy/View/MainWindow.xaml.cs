using System.Windows;
using PostExporter.View.Pages;

namespace PostExporter.View;

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