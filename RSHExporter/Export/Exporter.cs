using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;
using HtmlToOpenXml;
using RSHExporter.Scrape;
using RSHExporter.Utils;
using Thread = RSHExporter.Scrape.Thread;

namespace RSHExporter.Export;

public static class Exporter
{
    private static readonly HashSet<string> SingleParagraphTags = new()
    {
        "a", "abbr", "acronym", "b", "bdi", "bdo", "big", "cite", "code", "data", "del", "dfn", "em", "i", "ins", "kbd",
        "label", "mark", "q", "s", "samp", "small", "span", "strong", "sub", "sup", "time", "u", "var", "wbr"
    };

    public static async Task Export(List<Post> posts, StringBuilder textHeadTemplate, StringBuilder textBodyTemplate,
        StringBuilder htmlHeadTemplate, StringBuilder htmlBodyTemplate, CancellationToken cancellationToken)
    {
        var thread = posts[0].Thread;
        var group = thread.Group;

        var (directoryPath, imagesDirectoryPath, imagesDirectory, filePathWithoutExtension) =
            CreatePaths(group, thread);

        Directory.CreateDirectory(directoryPath);

        if (ExportConfiguration.IncludeImages)
        {
            if (ExportConfiguration.DownloadImages)
            {
                var directoryAlreadyCreated = false;

                for (int i = 0; i < posts.Count; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var post = posts[i];

                    var imageNodes = new List<HtmlNode>();
                    foreach (var textNode in post.TextNodes)
                    {
                        imageNodes.AddRange(textNode.SelectNodes(".//img"));
                    }

                    for (var j = 0; j < imageNodes.Count; ++j)
                    {
                        var imageNode = imageNodes[j];

                        var source = imageNode.Attributes["src"].Value;
                        var altName = imageNode.Attributes["alt"].Value;

                        var fileNameBuilder = new StringBuilder();
                        fileNameBuilder.Append($"{i:D3}-{j:D3}");

                        if (!string.IsNullOrEmpty(altName))
                        {
                            fileNameBuilder.Append('-').Append(HttpUtility.HtmlDecode(altName));
                            SanitizeFileName(fileNameBuilder);
                        }

                        var fileName = fileNameBuilder.ToString();

                        if (!directoryAlreadyCreated)
                        {
                            Directory.CreateDirectory(imagesDirectoryPath);
                            directoryAlreadyCreated = true;
                        }

                        var (success, fileNameWithExtension) =
                            await Scraper.DownloadImage(source, imagesDirectoryPath, fileName);

                        if (success)
                        {
                            imageNode.SetAttributeValue("srcHtml",
                                Path.Combine(imagesDirectory, fileNameWithExtension));
                            imageNode.SetAttributeValue("srcDocx",
                                Path.Combine(imagesDirectoryPath, fileNameWithExtension));
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var post in posts)
            {
                var imageNodes = new List<HtmlNode>();
                foreach (var textNode in post.TextNodes)
                {
                    imageNodes.AddRange(textNode.SelectNodes(".//img"));
                }

                foreach (var imageNode in imageNodes)
                {
                    imageNode.Remove();
                }
            }
        }

        foreach (var fileFormat in ExportConfiguration.FileFormats)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePathWithExtension = $"{filePathWithoutExtension}.{fileFormat.FileExtension()}";

            switch (fileFormat)
            {
                case FileFormat.Txt:
                {
                    var stringBuilder = CreateText(group, thread, posts, textHeadTemplate, textBodyTemplate,
                        cancellationToken);

                    await using var file = new StreamWriter(filePathWithExtension);
                    await file.WriteAsync(stringBuilder, cancellationToken);

                    break;
                }

                case FileFormat.Html:
                {
                    var stringBuilder = CreateHtml(group, thread, posts, fileFormat, htmlHeadTemplate, htmlBodyTemplate,
                        cancellationToken);

                    await using var file = new StreamWriter(filePathWithExtension);
                    await file.WriteAsync(stringBuilder, cancellationToken);

                    break;
                }

                case FileFormat.Docx:
                {
                    var stringBuilder = CreateHtml(group, thread, posts, fileFormat, htmlHeadTemplate, htmlBodyTemplate,
                        cancellationToken);

                    using var wordDocument =
                        WordprocessingDocument.Create(filePathWithExtension, WordprocessingDocumentType.Document);
                    var mainPart = wordDocument.AddMainDocumentPart();

                    var converter = new HtmlConverter(mainPart);
                    converter.ParseHtml(stringBuilder.ToString());

                    break;
                }

                default:
                    throw new NotSupportedException(fileFormat.ToString());
            }
        }
    }

    private static StringBuilder CreateText(Group group, Thread thread, List<Post> posts, StringBuilder headTemplate,
        StringBuilder bodyTemplate, CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder().Append(headTemplate);

        if (ExportConfiguration.IncludeGroup)
        {
            stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupTitle, group.Title);

            if (ExportConfiguration.IncludeGroupAuthor)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupAuthor, group.Author);
            }

            if (ExportConfiguration.IncludeGroupPostedAt)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupPostedAt,
                    group.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            if (ExportConfiguration.IncludeGroupUrl)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupUrl, group.Url);
            }
        }

        if (ExportConfiguration.IncludeThread)
        {
            stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadTitle, thread.Title);

            if (ExportConfiguration.IncludeThreadAuthor)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadAuthor, thread.Author);
            }

            if (ExportConfiguration.IncludeThreadPostedAt)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadPostedAt,
                    thread.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            if (ExportConfiguration.IncludeThreadUrl)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadUrl, thread.Url);
            }
        }

        var postsBuilder = new StringBuilder();

        for (var i = 0; i < posts.Count; ++i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var postNumber = ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i;
            var post = posts[postNumber];

            var postBuilder = new StringBuilder().Append(bodyTemplate);

            if (ExportConfiguration.IncludePostNumber)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderCurrentPostNumber,
                    postNumber.ToString());
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderTotalPostNumber,
                    posts.Count.ToString());
            }

            if (ExportConfiguration.IncludePostAuthor)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostAuthor, post.Author);
            }

            if (ExportConfiguration.IncludePostPostedAt)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostPostedAt,
                    post.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostText,
                GetTextFromNodes(post.TextNodes).ToString());
            postsBuilder.Append(postBuilder);
        }

        return stringBuilder.Replace(Resources.Localization.Resources.PlaceholderPosts, postsBuilder.ToString());
    }

    private static StringBuilder GetTextFromNodes(IEnumerable<HtmlNode> nodes)
    {
        var stringBuilder = new StringBuilder();
        GetTextFromNodes(stringBuilder, nodes);
        return stringBuilder;
    }

    private static void GetTextFromNodes(StringBuilder stringBuilder, IEnumerable<HtmlNode> nodes)
    {
        foreach (var child in nodes)
        {
            GetTextFromNodes(stringBuilder, child);
        }
    }

    private static void GetTextFromNodes(StringBuilder stringBuilder, HtmlNode node)
    {
        if (IsSingleParagraph(node))
        {
            if (node.Name == "img")
            {
                var source = node.Attributes["src"].Value;
                var altName = node.Attributes["alt"].Value;

                var text = string.IsNullOrEmpty(altName)
                    ? $"[{Resources.Localization.Resources.TemplateImageAt} {source}]"
                    : $"[{Util.CapitalizeFirstChar(HttpUtility.HtmlDecode(altName))} {Resources.Localization.Resources.TemplateAt} {source}]";

                stringBuilder.AppendLine(text);
            }
            else
            {
                var text = node.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    stringBuilder.AppendLine(HttpUtility.HtmlDecode(text));
                }
            }
        }
        else
        {
            GetTextFromNodes(stringBuilder, node.ChildNodes);
        }
    }

    private static bool IsSingleParagraph(HtmlNode node)
    {
        foreach (var child in node.ChildNodes)
        {
            return SingleParagraphTags.Contains(child.Name) && IsSingleParagraph(child);
        }

        return true;
    }

    private static StringBuilder CreateHtml(Group group, Thread thread, List<Post> posts, FileFormat fileFormat,
        StringBuilder headTemplate, StringBuilder bodyTemplate, CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder().Append(headTemplate);

        if (ExportConfiguration.IncludeGroup)
        {
            stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupTitle,
                HttpUtility.HtmlEncode(group.Title));

            if (ExportConfiguration.IncludeGroupAuthor)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupAuthor,
                    HttpUtility.HtmlEncode(group.Author));
            }

            if (ExportConfiguration.IncludeGroupPostedAt)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupPostedAt,
                    group.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            if (ExportConfiguration.IncludeGroupUrl)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderGroupUrl, group.Url);
            }
        }

        if (ExportConfiguration.IncludeThread)
        {
            stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadTitle,
                HttpUtility.HtmlEncode(thread.Title));

            if (ExportConfiguration.IncludeThreadAuthor)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadAuthor,
                    HttpUtility.HtmlEncode(thread.Author));
            }

            if (ExportConfiguration.IncludeThreadPostedAt)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadPostedAt,
                    thread.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            if (ExportConfiguration.IncludeThreadUrl)
            {
                stringBuilder.Replace(Resources.Localization.Resources.PlaceholderThreadUrl, thread.Url);
            }
        }

        var postsBuilder = new StringBuilder();

        for (var i = 0; i < posts.Count; ++i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var postNumber = ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i;
            var post = posts[postNumber];

            var postBuilder = new StringBuilder().Append(bodyTemplate);

            if (ExportConfiguration.IncludePostNumber)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderCurrentPostNumber,
                    postNumber.ToString());
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderTotalPostNumber,
                    posts.Count.ToString());
            }

            if (ExportConfiguration.IncludePostAuthor)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostAuthor,
                    HttpUtility.HtmlEncode(post.Author));
            }

            if (ExportConfiguration.IncludePostPostedAt)
            {
                postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostPostedAt,
                    post.PostedAt.ToString("dd.MM.yyyy HH:mm"));
            }

            postBuilder.Replace(Resources.Localization.Resources.PlaceholderPostText,
                GetHtmlFromNodes(post.TextNodes, fileFormat).ToString());
            postsBuilder.Append(postBuilder);
        }

        return stringBuilder.Replace(Resources.Localization.Resources.PlaceholderPosts, postsBuilder.ToString());
    }

    private static StringBuilder GetHtmlFromNodes(IEnumerable<HtmlNode> nodes, FileFormat fileFormat)
    {
        var textBuilder = new StringBuilder();
        foreach (var textNode in nodes)
        {
            if (ExportConfiguration.DownloadImages)
            {
                foreach (var imageNode in textNode.SelectNodes(".//img"))
                {
                    if (fileFormat == FileFormat.Html && imageNode.Attributes.Contains("srcHtml"))
                    {
                        imageNode.Attributes["src"].Value = imageNode.Attributes["srcHtml"].Value;
                    }
                    else if (fileFormat == FileFormat.Docx && imageNode.Attributes.Contains("srcDocx"))
                    {
                        imageNode.Attributes["src"].Value = imageNode.Attributes["srcDocx"].Value;
                    }
                }
            }

            textBuilder.Append(textNode.OuterHtml);
        }

        return textBuilder;
    }

    private static (string, string, string, string) CreatePaths(Group group, Thread thread)
    {
        var groupTitle = new StringBuilder(group.Title);
        SanitizeFileName(groupTitle);

        var threadTitle = new StringBuilder(thread.Title);
        SanitizeFileName(threadTitle);

        string directoryPath;
        StringBuilder fileName;

        if (ExportConfiguration.DownloadToOwnFolder)
        {
            directoryPath = Path.Combine(ExportConfiguration.ExportDirectoryPath, groupTitle.ToString());
            fileName = threadTitle;
        }
        else
        {
            directoryPath = ExportConfiguration.ExportDirectoryPath;
            fileName = new StringBuilder().Append(groupTitle).Append('-').Append(threadTitle);
        }

        string imagesDirectoryPath;
        string imagesDirectory;

        if (ExportConfiguration.DownloadImagesToOwnFolder)
        {
            var directoryName = fileName.ToString();
            imagesDirectoryPath = Path.Combine(directoryPath, directoryName);
            imagesDirectory = Path.Combine(".", directoryName);
        }
        else
        {
            imagesDirectoryPath = directoryPath;
            imagesDirectory = ".";
        }

        return (directoryPath, imagesDirectoryPath, imagesDirectory, Path.Join(directoryPath, fileName.ToString()));
    }

    private static void SanitizeFileName(StringBuilder stringBuilder)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            stringBuilder.Replace(c, '-');
        }
    }
}