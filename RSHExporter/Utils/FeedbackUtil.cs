using System;
using Sentry;

namespace RSHExporter.Utils;

public static class FeedbackUtil
{
    public static void HandleException(Exception exception)
    {
        var sentryId = SentrySdk.CaptureException(exception);

        var feedback = DialogUtil.ShowErrorFeedbackDialog(sentryId.ToString());

        if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
        {
            SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
        }
    }

    public static void HandleFeedback(string source)
    {
        var feedback = DialogUtil.ShowFeedbackDialog();

        if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
        {
            var sentryId =
                SentrySdk.CaptureMessage($"New feedback from {source}");
            SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
        }
    }
}