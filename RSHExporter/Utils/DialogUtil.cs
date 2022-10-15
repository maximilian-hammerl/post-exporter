using System;
using System.Windows;
using System.Windows.Media;

namespace RSHExporter.Utils;

public static class DialogUtil
{
    public static void ShowHelpAndHighlight(params (Action<Brush> updateBackgroundAction, string text)[] elements)
    {
        for (var i = 0; i < elements.Length; ++i)
        {
            var (border, text) = elements[i];

            if (!ShowHelpAndHighlight(border, text, i + 1, elements.Length))
            {
                break;
            }
        }
    }

    private static bool ShowHelpAndHighlight(Action<Brush> updateBackgroundAction, string text, int currentStep,
        int numberSteps)
    {
        updateBackgroundAction(Brushes.Yellow);
        var continueWithHelp = ShowHelp(text, currentStep, numberSteps);
        updateBackgroundAction(Brushes.White);

        return continueWithHelp;
    }

    private static bool ShowHelp(string text, int currentStep, int numberSteps)
    {
        // Last step
        if (currentStep == numberSteps)
        {
            MessageBox.Show(text, $"Step {currentStep} of {numberSteps}", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }

        var result = MessageBox.Show(text, $"Step {currentStep} of {numberSteps}", MessageBoxButton.OKCancel,
            MessageBoxImage.Information);
        return result == MessageBoxResult.OK;
    }

    public static (string Response, string Email)? ShowFeedbackDialog()
    {
        return ShowFeedbackDialog(FeedbackDialog.FeedbackType.Default);
    }

    public static (string Response, string Email)? ShowErrorFeedbackDialog(string id)
    {
        return ShowFeedbackDialog(FeedbackDialog.FeedbackType.Error, id);
    }

    private static (string Response, string Email)? ShowFeedbackDialog(FeedbackDialog.FeedbackType feedbackType,
        string? id = null)
    {
        var feedbackDialog = new FeedbackDialog(feedbackType, id);
        feedbackDialog.ShowDialog();

        return feedbackDialog.DialogResult is true ? (feedbackDialog.Response, feedbackDialog.Email) : null;
    }

    public static bool ShowQuestion(string text, string caption = "Information")
    {
        return MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Information) ==
               MessageBoxResult.Yes;
    }

    public static void ShowInformation(string text, string caption = "Information")
    {
        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void ShowWarning(string text, string caption = "Warning")
    {
        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static void ShowError(string text, string caption = "Error")
    {
        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}