using System;
using System.Windows;

namespace RSHExporter.View;

public partial class FeedbackDialog : Window
{
    public enum FeedbackType
    {
        Default,
        Error
    }

    public FeedbackDialog(FeedbackType feedbackType, string? id = null)
    {
        InitializeComponent();

        TitleTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => RSHExporter.Resources.Localization.Resources.FeedbackTitleDefault,
            FeedbackType.Error => RSHExporter.Resources.Localization.Resources.FeedbackTitleError,
            _ => throw new ArgumentOutOfRangeException(nameof(feedbackType), feedbackType, @"Unknown value")
        };

        DetailsTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => RSHExporter.Resources.Localization.Resources.FeedbackDetailsDefault,
            FeedbackType.Error => string.Format(RSHExporter.Resources.Localization.Resources.FeedbackDetailsError, id),
            _ => throw new ArgumentOutOfRangeException(nameof(feedbackType), feedbackType, @"Unknown value")
        };

        ResponseTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => RSHExporter.Resources.Localization.Resources.FeedbackResponseDefault,
            FeedbackType.Error => RSHExporter.Resources.Localization.Resources.FeedbackResponseError,
            _ => throw new ArgumentOutOfRangeException(nameof(feedbackType), feedbackType, @"Unknown value")
        };

        EmailTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => RSHExporter.Resources.Localization.Resources.FeedbackEmailDefault,
            FeedbackType.Error => RSHExporter.Resources.Localization.Resources.FeedbackEmailError,
            _ => throw new ArgumentOutOfRangeException(nameof(feedbackType), feedbackType, @"Unknown value")
        };

        ResponseTextBox.Focus();
    }

    public string Response => ResponseTextBox.Text;
    public string Email => EmailTextBox.Text;

    private void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}