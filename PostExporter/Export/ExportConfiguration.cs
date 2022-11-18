using System;
using System.Collections.Generic;
using System.Text;
using PostExporter.Utils;

namespace PostExporter.Export;

public static class ExportConfiguration
{
    // Config options for templates
    public static StringBuilder? TextHeadTemplate { get; set; } = null;
    public static StringBuilder? TextBodyTemplate { get; set; } = null;
    public static StringBuilder? HtmlHeadTemplate { get; set; } = null;
    public static StringBuilder? HtmlBodyTemplate { get; set; } = null;

    // Config options configured by user
    public static string? DirectoryPath { get; set; } = null;

    public static string ExportDirectoryPath
    {
        get
        {
            if (DirectoryPath == null)
            {
                throw new ArgumentException("Directory path is missing");
            }

            return DirectoryPath;
        }
    }

    public static List<FileFormat> FileFormats { get; set; } = new(3) { FileFormat.Html };
    public static bool IncludeGroup { get; set; } = false;
    public static bool IncludeGroupAuthor { get; set; } = false;
    public static bool IncludeGroupPostedAt { get; set; } = false;
    public static bool IncludeGroupUrl { get; set; } = false;
    public static bool IncludeThread { get; set; } = false;
    public static bool IncludeThreadAuthor { get; set; } = false;
    public static bool IncludeThreadPostedAt { get; set; } = false;
    public static bool IncludeThreadUrl { get; set; } = false;
    public static bool IncludePostAuthor { get; set; } = false;
    public static bool IncludePostPostedAt { get; set; } = false;
    public static bool IncludePostNumber { get; set; } = false;
    public static bool IncludeImages { get; set; } = true;
    public static bool ReserveOrder { get; set; } = false;
    public static bool UseCustomTemplates { get; set; } = false;
    public static bool DownloadToOwnFolder { get; set; } = true;
    public static bool DownloadImages { get; set; } = true;
    public static bool DownloadImagesToOwnFolder { get; set; } = true;

    // Other config options
    public static HashSet<int> GroupIdsWithSameTitle { get; } = new();
    public static HashSet<int> ThreadIdsWithSameTitle { get; } = new();

    public static StringBuilder GetDefaultTextHeadTemplate(bool withAllPlaceholders = false)
    {
        var stringBuilder = new StringBuilder();

        if (IncludeGroup || withAllPlaceholders)
        {
            stringBuilder.AppendLine(Resources.Localization.Resources.PlaceholderGroupTitle);

            if (CreateSubtitleLine(IncludeGroupAuthor || withAllPlaceholders,
                    IncludeGroupPostedAt || withAllPlaceholders,
                    Resources.Localization.Resources.PlaceholderGroupAuthor,
                    Resources.Localization.Resources.PlaceholderGroupPostedAt, out var line))
            {
                stringBuilder.AppendLine(line);
            }

            if (IncludeGroupUrl || withAllPlaceholders)
            {
                stringBuilder.AppendLine($"URL: {Resources.Localization.Resources.PlaceholderGroupUrl}");
            }

            stringBuilder.AppendLine();
        }

        if (IncludeThread || withAllPlaceholders)
        {
            stringBuilder.AppendLine(Resources.Localization.Resources.PlaceholderThreadTitle);

            if (CreateSubtitleLine(IncludeThreadAuthor || withAllPlaceholders,
                    IncludeThreadPostedAt || withAllPlaceholders,
                    Resources.Localization.Resources.PlaceholderThreadAuthor,
                    Resources.Localization.Resources.PlaceholderThreadPostedAt, out var line))
            {
                stringBuilder.AppendLine(line);
            }

            if (IncludeThreadUrl || withAllPlaceholders)
            {
                stringBuilder.AppendLine($"URL: {Resources.Localization.Resources.PlaceholderThreadUrl}");
            }

            stringBuilder.AppendLine();
        }

        return stringBuilder.AppendLine(Resources.Localization.Resources.PlaceholderPosts);
    }

    public static StringBuilder GetDefaultTextBodyTemplate(bool withAllPlaceholders = false)
    {
        var stringBuilder = new StringBuilder();

        if (CreateSubtitleLine(IncludePostNumber || withAllPlaceholders,
                IncludePostAuthor || withAllPlaceholders,
                IncludePostPostedAt || withAllPlaceholders,
                Resources.Localization.Resources.PlaceholderCurrentPostNumber,
                Resources.Localization.Resources.PlaceholderTotalPostNumber,
                Resources.Localization.Resources.PlaceholderPostAuthor,
                Resources.Localization.Resources.PlaceholderPostPostedAt, out var line))
        {
            stringBuilder.AppendLine(line);
        }

        return stringBuilder.AppendLine(Resources.Localization.Resources.PlaceholderPostText).AppendLine();
    }

    public static StringBuilder GetDefaultHtmlHeadTemplate(bool withAllPlaceholders = false)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder
            .AppendLine("<html>")
            .AppendLine("<head>")
            .AppendLine("<title>")
            .AppendLine("Post Export")
            .AppendLine("</title>")
            .AppendLine("</head>")
            .AppendLine("<body>");

        if (IncludeGroup || withAllPlaceholders)
        {
            stringBuilder
                .Append("<h3>")
                .Append(Resources.Localization.Resources.PlaceholderGroupTitle)
                .AppendLine("</h3>");

            if (CreateSubtitleLine(IncludeGroupAuthor || withAllPlaceholders,
                    IncludeGroupPostedAt || withAllPlaceholders,
                    Resources.Localization.Resources.PlaceholderGroupAuthor,
                    Resources.Localization.Resources.PlaceholderGroupPostedAt, out var line))
            {
                stringBuilder
                    .Append("<p>")
                    .Append(line)
                    .AppendLine("</p>");
            }

            if (IncludeGroupUrl || withAllPlaceholders)
            {
                stringBuilder
                    .Append("<p>URL: <a href=\"")
                    .Append(Resources.Localization.Resources.PlaceholderGroupUrl)
                    .Append("\">")
                    .Append(Resources.Localization.Resources.PlaceholderGroupUrl)
                    .AppendLine("</a></p>");
            }
        }

        if (IncludeThread || withAllPlaceholders)
        {
            stringBuilder
                .Append("<h2>")
                .Append(Resources.Localization.Resources.PlaceholderThreadTitle)
                .AppendLine("</h2>");

            if (CreateSubtitleLine(IncludeThreadAuthor || withAllPlaceholders,
                    IncludeThreadPostedAt || withAllPlaceholders,
                    Resources.Localization.Resources.PlaceholderThreadAuthor,
                    Resources.Localization.Resources.PlaceholderThreadPostedAt, out var line))
            {
                stringBuilder
                    .Append("<p>")
                    .Append(line)
                    .AppendLine("</p>");
            }

            if (IncludeThreadUrl || withAllPlaceholders)
            {
                stringBuilder
                    .Append("<p>URL: <a href=\"")
                    .Append(Resources.Localization.Resources.PlaceholderThreadUrl)
                    .Append("\">")
                    .Append(Resources.Localization.Resources.PlaceholderThreadUrl)
                    .AppendLine("</a></p>");
            }
        }

        return stringBuilder
            .AppendLine(Resources.Localization.Resources.PlaceholderPosts)
            .AppendLine("</body>")
            .AppendLine("</html>");
    }

    public static StringBuilder GetDefaultHtmlBodyTemplate(bool withAllPlaceholders = false)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder
            .AppendLine("<div>");

        if (CreateSubtitleLine(IncludePostNumber || withAllPlaceholders,
                IncludePostAuthor || withAllPlaceholders,
                IncludePostPostedAt || withAllPlaceholders,
                Resources.Localization.Resources.PlaceholderCurrentPostNumber,
                Resources.Localization.Resources.PlaceholderTotalPostNumber,
                Resources.Localization.Resources.PlaceholderPostAuthor,
                Resources.Localization.Resources.PlaceholderPostPostedAt, out var line))
        {
            stringBuilder
                .Append("<h4>")
                .Append(line)
                .AppendLine("</h4>");
        }

        return stringBuilder
            .AppendLine("<div>")
            .AppendLine(Resources.Localization.Resources.PlaceholderPostText)
            .AppendLine("</div>")
            .AppendLine("</div>")
            .AppendLine("<br/>");
    }

    private static bool CreateSubtitleLine(bool includeAuthor, bool includePostedAt, string authorPlaceholder,
        string postedAtPlaceholder, out string? line)
    {
        var headerElements = new List<string>(2);

        if (includeAuthor)
        {
            headerElements.Add($"{Resources.Localization.Resources.TemplateFrom} {authorPlaceholder}");
        }

        if (includePostedAt)
        {
            headerElements.Add($"{Resources.Localization.Resources.TemplateOn} {postedAtPlaceholder}");
        }

        if (headerElements.Count > 0)
        {
            line = Util.CapitalizeFirstChar(string.Join(" ", headerElements));
            return true;
        }

        line = default;
        return false;
    }

    private static bool CreateSubtitleLine(bool includePostNumber, bool includeAuthor, bool includePostedAt,
        string currentPostNumberPlaceholder, string totalPostNumberPlaceholder, string authorPlaceholder,
        string postedAtPlaceholder, out string? line)
    {
        var headerElements = new List<string>(3);

        if (includePostNumber)
        {
            headerElements.Add($"({currentPostNumberPlaceholder}/{totalPostNumberPlaceholder})");
        }

        if (includeAuthor)
        {
            headerElements.Add($"{Resources.Localization.Resources.TemplateFrom} {authorPlaceholder}");
        }

        if (includePostedAt)
        {
            headerElements.Add($"{Resources.Localization.Resources.TemplateOn} {postedAtPlaceholder}");
        }

        if (headerElements.Count > 0)
        {
            line = Util.CapitalizeFirstChar(string.Join(" ", headerElements));
            return true;
        }

        line = default;
        return false;
    }
}