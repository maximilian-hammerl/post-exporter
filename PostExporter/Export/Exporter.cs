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
using PostExporter.Scrape;
using PostExporter.Utils;
using Thread = PostExporter.Scrape.Thread;

namespace PostExporter.Export;

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

        var (directoryPath, imagesDirectoryPath, imagesDirectory, imageFileNamePrefix, filePathWithoutExtension) =
            CreatePaths(group, thread);

        Directory.CreateDirectory(directoryPath);

        if (ExportConfiguration.IncludeImages)
        {
            if (ExportConfiguration.DownloadImages)
            {
                await DownloadImagesAndUpdateImageSources(posts, imagesDirectoryPath, imagesDirectory,
                    imageFileNamePrefix,
                    cancellationToken);
            }
            else
            {
                await UpdateImageSources(posts, ExportConfiguration.FileFormats.Contains(FileFormat.Docx),
                    cancellationToken);
            }
        }
        else
        {
            RemoveImages(posts);
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

    private static async Task DownloadImagesAndUpdateImageSources(List<Post> posts, string imagesDirectoryPath,
        string imagesDirectory,
        string imageFileNamePrefix, CancellationToken cancellationToken)
    {
        var directoryAlreadyCreated = false;

        for (var i = 0; i < posts.Count; ++i)
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

                var fileNameBuilder = new StringBuilder(imageFileNamePrefix);
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
                    // HTML can use relative paths
                    imageNode.SetAttributeValue("srcHtml",
                        Path.Combine(imagesDirectory, fileNameWithExtension));
                    // DOCX requires absolute paths
                    imageNode.SetAttributeValue("srcDocx",
                        Path.Combine(imagesDirectoryPath, fileNameWithExtension));
                }
                else
                {
                    // HTML has no problem with (possibly temporarily) failing image sources
                    imageNode.SetAttributeValue("srcHtml", source);
                    // DOCX will crash with failing images sources
                    imageNode.SetAttributeValue("srcDocx", "");
                }
            }
        }
    }

    private static async Task UpdateImageSources(List<Post> posts, bool testImageSources,
        CancellationToken cancellationToken)
    {
        foreach (var post in posts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var textNode in post.TextNodes)
            {
                foreach (var imageNode in textNode.SelectNodes(".//img"))
                {
                    var source = imageNode.Attributes["src"].Value;

                    var success = !testImageSources || await Scraper.TestImage(source);

                    // HTML has no problem with (possibly temporarily) failing image sources
                    imageNode.SetAttributeValue("srcHtml", source);
                    // DOCX will crash with failing images sources
                    imageNode.SetAttributeValue("srcDocx", success ? source : "");
                }
            }
        }
    }

    private static void RemoveImages(List<Post> posts)
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
                    (postNumber + 1).ToString());
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
                    (postNumber + 1).ToString());
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
                    if (fileFormat == FileFormat.Html)
                    {
                        imageNode.Attributes["src"].Value = imageNode.Attributes["srcHtml"].Value;
                    }
                    else if (fileFormat == FileFormat.Docx)
                    {
                        imageNode.Attributes["src"].Value = imageNode.Attributes["srcDocx"].Value;
                    }
                }
            }

            textBuilder.Append(textNode.OuterHtml);
        }

        return textBuilder;
    }

    private static (string, string, string, string, string) CreatePaths(Group group, Thread thread)
    {
        var groupTitleBuilder = CreateTitleBuilder(group.Title, group.Id,
            ExportConfiguration.GroupIdsWithSameTitle.Contains(group.Id), Resources.Localization.Resources.Group);

        var threadTitleBuilder = CreateTitleBuilder(thread.Title, thread.Id,
            ExportConfiguration.ThreadIdsWithSameTitle.Contains(thread.Id), Resources.Localization.Resources.Thread);

        string directoryPath;
        StringBuilder fileNameBuilder;

        if (ExportConfiguration.DownloadToOwnFolder)
        {
            directoryPath = Path.Combine(ExportConfiguration.ExportDirectoryPath, groupTitleBuilder.ToString());
            fileNameBuilder = threadTitleBuilder;
        }
        else
        {
            directoryPath = ExportConfiguration.ExportDirectoryPath;
            fileNameBuilder = new StringBuilder().Append(groupTitleBuilder).Append('-').Append(threadTitleBuilder);
        }

        var fileName = fileNameBuilder.ToString();

        string imagesDirectoryPath;
        string imagesDirectory;
        string imageFileNamePrefix;

        if (ExportConfiguration.DownloadImagesToOwnFolder)
        {
            imagesDirectoryPath = Path.Combine(directoryPath, fileName);
            imagesDirectory = Path.Combine(".", fileName);
            imageFileNamePrefix = "";
        }
        else
        {
            imagesDirectoryPath = directoryPath;
            imagesDirectory = ".";
            imageFileNamePrefix = $"{fileName}-";
        }

        return (directoryPath, imagesDirectoryPath, imagesDirectory, imageFileNamePrefix,
            Path.Join(directoryPath, fileName));
    }

    private static StringBuilder CreateTitleBuilder(string title, int id, bool includeId, string postfix)
    {
        var titleBuilder = new StringBuilder(title);
        titleBuilder.SanitizeFileName().Trim();

        if (includeId)
        {
            titleBuilder.Append(" (").Append(id).Append(") ");
        }
        else
        {
            titleBuilder.Append(' ');
        }

        titleBuilder.Append(postfix);

        return titleBuilder;
    }

    private static StringBuilder SanitizeFileName(this StringBuilder stringBuilder)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            stringBuilder.Replace(c, '-');
        }

        return stringBuilder;
    }

    private static StringBuilder Trim(this StringBuilder stringBuilder)
    {
        var onlyWhiteSpaces = true;

        for (var start = 0; start < stringBuilder.Length; start++)
        {
            if (char.IsWhiteSpace(stringBuilder[start]))
            {
                continue;
            }

            if (start > 0)
            {
                stringBuilder.Remove(0, start);
            }

            onlyWhiteSpaces = false;
            break;
        }

        if (onlyWhiteSpaces)
        {
            stringBuilder.Clear();
            return stringBuilder;
        }

        for (var end = stringBuilder.Length - 1; end >= 0; end--)
        {
            if (char.IsWhiteSpace(stringBuilder[end]))
            {
                continue;
            }

            if (end < stringBuilder.Length - 1)
            {
                stringBuilder.Remove(end + 1, stringBuilder.Length - 1 - end);
            }

            break;
        }

        return stringBuilder;
    }
}