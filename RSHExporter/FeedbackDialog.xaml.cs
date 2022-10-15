using System;
using System.Windows;

namespace RSHExporter;

public partial class FeedbackDialog : Window
{
    public enum FeedbackType
    {
        Default,
        Error,
    }

    public FeedbackDialog(FeedbackType feedbackType, string? id = null)
    {
        InitializeComponent();

        TitleTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => "Thanks for your feedback!",
            FeedbackType.Error => "An error has occurred!",
            _ => throw new NotSupportedException(feedbackType.ToString())
        };

        DetailsTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => $"Your feedback will be sent to Maxi You can also, e.g., include links to images.",
            FeedbackType.Error => $"Something went wrong unexpectedly! The error has been reported with the ID {id}.",
            _ => throw new NotSupportedException(feedbackType.ToString())
        };

        ResponseTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => "Please enter your feedback here:",
            FeedbackType.Error => "If possible, please enter more details what you did before the error occured here:",
            _ => throw new NotSupportedException(feedbackType.ToString())
        };

        EmailTextBlock.Text = feedbackType switch
        {
            FeedbackType.Default => "If you want to possibly be contacted, enter your email address here:",
            FeedbackType.Error =>
                "If you want to be notified by email when the error is fixed, enter your email address here:",
            _ => throw new NotSupportedException(feedbackType.ToString())
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