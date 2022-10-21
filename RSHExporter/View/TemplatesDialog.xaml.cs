using System.Text;
using System.Windows;
using RSHExporter.Export;

namespace RSHExporter.View;

public partial class TemplatesDialog : Window
{
    public TemplatesDialog()
    {
        InitializeComponent();

        TextHeadTextBox.Text =
            (ExportConfiguration.TextHeadTemplate ?? ExportConfiguration.GetDefaultTextHeadTemplate()).ToString();
        TextBodyTextBox.Text =
            (ExportConfiguration.TextBodyTemplate ?? ExportConfiguration.GetDefaultTextBodyTemplate()).ToString();
        HtmlHeadTextBox.Text =
            (ExportConfiguration.HtmlHeadTemplate ?? ExportConfiguration.GetDefaultHtmlHeadTemplate()).ToString();
        HtmlBodyTextBox.Text =
            (ExportConfiguration.HtmlBodyTemplate ?? ExportConfiguration.GetDefaultHtmlBodyTemplate()).ToString();
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExportConfiguration.TextHeadTemplate = new StringBuilder(TextHeadTextBox.Text);
        ExportConfiguration.TextBodyTemplate = new StringBuilder(TextBodyTextBox.Text);
        ExportConfiguration.HtmlHeadTemplate = new StringBuilder(HtmlHeadTextBox.Text);
        ExportConfiguration.HtmlBodyTemplate = new StringBuilder(HtmlBodyTextBox.Text);
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResetCurrentPlaceholdersButton_OnClick(object sender, RoutedEventArgs e)
    {
        TextHeadTextBox.Text = ExportConfiguration.GetDefaultTextHeadTemplate().ToString();
        TextBodyTextBox.Text = ExportConfiguration.GetDefaultTextBodyTemplate().ToString();
        HtmlHeadTextBox.Text = ExportConfiguration.GetDefaultHtmlHeadTemplate().ToString();
        HtmlBodyTextBox.Text = ExportConfiguration.GetDefaultHtmlBodyTemplate().ToString();
    }

    private void ResetAllPlaceholdersButton_OnClick(object sender, RoutedEventArgs e)
    {
        TextHeadTextBox.Text = ExportConfiguration.GetDefaultTextHeadTemplate(true).ToString();
        TextBodyTextBox.Text = ExportConfiguration.GetDefaultTextBodyTemplate(true).ToString();
        HtmlHeadTextBox.Text = ExportConfiguration.GetDefaultHtmlHeadTemplate(true).ToString();
        HtmlBodyTextBox.Text = ExportConfiguration.GetDefaultHtmlBodyTemplate(true).ToString();
    }
}