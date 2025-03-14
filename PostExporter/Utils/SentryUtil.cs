using System;
using System.Collections.Generic;
using PostExporter.Scrape;
using PostExporter.View.Dialogs;
using Sentry;
using Thread = System.Threading.Thread;

namespace PostExporter.Utils;

public static class SentryUtil
{
    private static bool _collectDataAccepted;

    public static bool CollectDataAccepted
    {
        get => _collectDataAccepted;
        set
        {
            _collectDataAccepted = value;

            if (_collectDataAccepted)
            {
                InitializeSentry();
            }
        }
    }

    private static void InitializeSentry()
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
        if (CollectDataAccepted)
        {
            SentrySdk.CaptureException(exception);
        }
    }

    public static void HandleMessage(string? message)
    {
        if (CollectDataAccepted && !string.IsNullOrEmpty(message))
        {
            SentrySdk.CaptureMessage($"{message} (base URL: {Scraper.BaseUrl}");
        }
    }

    public static void HandleBreadcrumb(string message, string? category = null, string? type = null,
        IDictionary<string, string>? data = null, BreadcrumbLevel level = default)
    {
        if (CollectDataAccepted)
        {
            SentrySdk.AddBreadcrumb($"{message} (base URL: {Scraper.BaseUrl}", category, type, data, level);
        }
    }

    public static void UpdateUser(string username)
    {
        if (CollectDataAccepted)
        {
            var otherData = new Dictionary<string, string>
            {
                ["Culture"] = Thread.CurrentThread.CurrentUICulture.Name
            };

            if (!string.IsNullOrEmpty(Scraper.BaseUrl))
            {
                otherData["BaseUrl"] = Scraper.BaseUrl;
            }

            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new SentryUser
                {
                    Username = username,
                    Other = otherData
                };
            });
        }
    }

    public static void HandleFeedbackForException(Exception exception)
    {
        if (CollectDataAccepted)
        {
            var sentryId = SentrySdk.CaptureException(exception);

            var feedback = ShowFeedbackDialog(FeedbackDialog.FeedbackType.Error, sentryId.ToString());

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                SentrySdk.CaptureFeedback(
                    feedback.Value.Response,
                    contactEmail: feedback.Value.Email,
                    name: feedback.Value.Username,
                    associatedEventId: sentryId
                );
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorGeneric, true);
        }
    }

    public static void HandleFeedback(string source)
    {
        if (CollectDataAccepted)
        {
            var feedback = ShowFeedbackDialog();

            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.Value.Response))
            {
                var sentryId = SentrySdk.CaptureMessage($"New feedback from {source}");
                SentrySdk.CaptureFeedback(
                    feedback.Value.Response,
                    contactEmail: feedback.Value.Email,
                    name: feedback.Value.Username,
                    associatedEventId: sentryId
                );
            }
        }
        else
        {
            DialogUtil.ShowError(Resources.Localization.Resources.ErrorNoSentry, false);
        }
    }

    private static (string Response, string Username, string Email)? ShowFeedbackDialog(
        FeedbackDialog.FeedbackType feedbackType = FeedbackDialog.FeedbackType.Default, string? id = null)
    {
        var feedbackDialog = new FeedbackDialog(feedbackType, id);
        feedbackDialog.ShowDialog();

        return feedbackDialog.DialogResult is true
            ? (feedbackDialog.Response, feedbackDialog.Username, feedbackDialog.Email)
            : null;
    }
}