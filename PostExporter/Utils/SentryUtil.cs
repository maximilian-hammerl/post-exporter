using System;
using System.Collections.Generic;
using PostExporter.View.Dialogs;
using Sentry;

namespace PostExporter.Utils;

public static class SentryUtil
{
    public static void InitializeSentry()
    {
        SentrySdk.Init(o =>
        {
            o.Dsn = "https://57c119ee365d4707b7f99e2b36c8782d@o1430708.ingest.sentry.io/6781778";
            o.TracesSampleRate = 1.0;
            o.IsGlobalModeEnabled = true;
            o.Release = $"{Util.GetAppName()}@{Util.GetCurrentVersionAsString(4)}";
            o.AutoSessionTracking = true;
        });
    }

    public static void HandleException(Exception exception)
    {
        if (ApplicationConfiguration.CollectDataAccepted)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    public static void HandleMessage(string? message)
    {
        if (ApplicationConfiguration.CollectDataAccepted && !string.IsNullOrEmpty(message))
        {
            SentrySdk.CaptureMessage(message);
        }
    }

    public static void HandleBreadcrumb(string message, string? category = null, string? type = null,
        IDictionary<string, string>? data = null, BreadcrumbLevel level = default)
    {
        if (ApplicationConfiguration.CollectDataAccepted)
        {
            SentrySdk.AddBreadcrumb(message, category, type, data, level);
        }
    }

    public static void UpdateUser(string username)
    {
        if (ApplicationConfiguration.CollectDataAccepted)
        {
            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new User
                {
                    Username = username
                };
            });
        }
    }

    public static void HandleFeedbackForException(Exception exception)
    {
        if (ApplicationConfiguration.CollectDataAccepted)
        {
            var sentryId = SentrySdk.CaptureException(exception);

            var feedback = ShowFeedbackDialog(FeedbackDialog.FeedbackType.Error, sentryId.ToString());

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorGeneric, true);
        }
    }

    public static void HandleFeedback(string source)
    {
        if (ApplicationConfiguration.CollectDataAccepted)
        {
            var feedback = ShowFeedbackDialog();

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                var sentryId =
                    SentrySdk.CaptureMessage($"New feedback from {source}");
                SentrySdk.CaptureUserFeedback(sentryId, feedback.Value.Email, feedback.Value.Response);
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorNoSentry, false);
        }
    }

    private static (string Response, string Email)? ShowFeedbackDialog(
        FeedbackDialog.FeedbackType feedbackType = FeedbackDialog.FeedbackType.Default, string? id = null)
    {
        var feedbackDialog = new FeedbackDialog(feedbackType, id);
        feedbackDialog.ShowDialog();

        return feedbackDialog.DialogResult is true ? (feedbackDialog.Response, feedbackDialog.Email) : null;
    }
}