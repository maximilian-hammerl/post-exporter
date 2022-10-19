using System;
using RSHExporter.View.Pages;
using Sentry;

namespace RSHExporter.Utils;

public static class FeedbackUtil
{
    public static void HandleFeedbackForException(Exception exception)
    {
        if (WelcomePage.CollectDataAccepted)
        {
            var sentryId = SentrySdk.CaptureException(exception);

            var feedback = DialogUtil.ShowErrorFeedbackDialog(sentryId.ToString());

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorGenericNoSentry);
        }
    }

    public static void HandleFeedback(string source)
    {
        if (WelcomePage.CollectDataAccepted)
        {
            var feedback = DialogUtil.ShowFeedbackDialog();

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                var sentryId =
                    SentrySdk.CaptureMessage($"New feedback from {source}");
                SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorNoSentry);
        }
    }
}