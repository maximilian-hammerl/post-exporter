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
using Thread = RSHExporter.Scrape.Thread;

namespace RSHExporter.Export;

public static class Exporter
{
    private static readonly HashSet<string> SingleParagraphTags = new()
    {
        "a", "abbr", "acronym", "b", "bdi", "bdo", "big", "cite", "code", "data", "del", "dfn", "em", "i", "ins", "kbd",
        "label", "mark", "q", "s", "samp", "small", "span", "strong", "sub", "sup", "time", "u", "var", "wbr"
    };

    public static async Task Export(List<Post> posts, CancellationToken cancellationToken)
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
                    var stringBuilder = CreateText(group, thread, posts, cancellationToken);

                    await using var file = new StreamWriter(filePathWithExtension);
                    await file.WriteAsync(stringBuilder, cancellationToken);

                    break;
                }

                case FileFormat.Html:
                {
                    var stringBuilder = CreateHtml(group, thread, posts, fileFormat, cancellationToken);

                    await using var file = new StreamWriter(filePathWithExtension);
                    await file.WriteAsync(stringBuilder, cancellationToken);

                    break;
                }

                case FileFormat.Docx:
                {
                    var stringBuilder = CreateHtml(group, thread, posts, fileFormat, cancellationToken);

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

    private static StringBuilder CreateText(Group group, Thread thread, List<Post> posts,
        CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder();

        if (ExportConfiguration.IncludeGroup)
        {
            stringBuilder.AppendLine(group.Title);

            if (CreateHeaderLine(group, ExportConfiguration.IncludeGroupAuthor,
                    ExportConfiguration.IncludeGroupPostedAt, out var header))
            {
                stringBuilder.AppendLine(header);
            }

            if (ExportConfiguration.IncludeGroupUrl)
            {
                stringBuilder.AppendLine($"URL: {group.Url}");
            }

            stringBuilder.AppendLine();
        }

        if (ExportConfiguration.IncludeThread)
        {
            stringBuilder.AppendLine(thread.Title);

            if (CreateHeaderLine(thread, ExportConfiguration.IncludeThreadAuthor,
                    ExportConfiguration.IncludeThreadPostedAt, out var header))
            {
                stringBuilder.AppendLine(header);
            }

            if (ExportConfiguration.IncludeThreadUrl)
            {
                stringBuilder.AppendLine($"URL: {thread.Url}");
            }

            stringBuilder.AppendLine();
        }

        for (var i = 0; i < posts.Count; ++i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var postNumber = ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i;
            var post = posts[postNumber];

            if (CreateHeaderLine(post, postNumber + 1, posts.Count, out var header))
            {
                stringBuilder.AppendLine(header);
            }

            WriteLines(stringBuilder, post.TextNodes);
            stringBuilder.AppendLine();
        }

        return stringBuilder;
    }

    private static void WriteLines(StringBuilder stringBuilder, IEnumerable<HtmlNode> nodes)
    {
        foreach (var child in nodes)
        {
            WriteLines(stringBuilder, child);
        }
    }

    private static void WriteLines(StringBuilder stringBuilder, HtmlNode node)
    {
        if (IsSingleParagraph(node))
        {
            if (node.Name == "img")
            {
                var source = node.Attributes["src"].Value;
                var altName = node.Attributes["alt"].Value;

                var text = string.IsNullOrEmpty(altName)
                    ? $"[Image at {source}]"
                    : $"[{CapitalizeFirstChar(HttpUtility.HtmlDecode(altName))} at {source}]";

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
            WriteLines(stringBuilder, node.ChildNodes);
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
        CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder
            .Append("<html><head><title>")
            .Append(HttpUtility.HtmlEncode(thread.Title))
            .Append("</title></head><body>");

        if (ExportConfiguration.IncludeGroup)
        {
            stringBuilder
                .Append("<h3>")
                .Append(HttpUtility.HtmlEncode(group.Title))
                .Append("</h3>");

            if (CreateHeaderLine(group, ExportConfiguration.IncludeGroupAuthor,
                    ExportConfiguration.IncludeGroupPostedAt, out var header))
            {
                stringBuilder
                    .Append("<p>")
                    .Append(HttpUtility.HtmlEncode(header))
                    .Append("</p>");
            }

            if (ExportConfiguration.IncludeGroupUrl)
            {
                stringBuilder
                    .Append("<p>URL: <a href=\"")
                    .Append(group.Url)
                    .Append("\">")
                    .Append(group.Url)
                    .Append("</a></p>");
            }
        }

        if (ExportConfiguration.IncludeThread)
        {
            stringBuilder
                .Append("<h3>")
                .Append(HttpUtility.HtmlEncode(thread.Title))
                .Append("</h3>");

            if (CreateHeaderLine(thread, ExportConfiguration.IncludeThreadAuthor,
                    ExportConfiguration.IncludeThreadPostedAt, out var header))
            {
                stringBuilder
                    .Append("<p>")
                    .Append(HttpUtility.HtmlEncode(header))
                    .Append("</p>");
            }

            if (ExportConfiguration.IncludeThreadUrl)
            {
                stringBuilder
                    .Append("<p>URL: <a href=\"")
                    .Append(thread.Url)
                    .Append("\">")
                    .Append(thread.Url)
                    .Append("</a></p>");
            }
        }

        for (var i = 0; i < posts.Count; ++i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var postNumber = ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i;
            var post = posts[postNumber];

            stringBuilder.Append("<div>");

            if (CreateHeaderLine(post, postNumber + 1, posts.Count, out var header))
            {
                stringBuilder
                    .Append("<p><strong>")
                    .Append(HttpUtility.HtmlEncode(header))
                    .Append("</strong></p>");
            }

            foreach (var textNode in post.TextNodes)
            {
                foreach (var imageNode in textNode.SelectNodes(".//img"))
                {
                    imageNode.Attributes["src"].Value = fileFormat switch
                    {
                        FileFormat.Html => imageNode.Attributes["srcHtml"].Value,
                        FileFormat.Docx => imageNode.Attributes["srcDocx"].Value,
                        _ => throw new NotSupportedException(fileFormat.ToString())
                    };
                }

                stringBuilder.Append(textNode.OuterHtml);
            }

            stringBuilder.Append("</div><br/>");
        }

        stringBuilder.Append("</body></html>");

        return stringBuilder;
    }

    private static bool CreateHeaderLine(PostedText postedText, bool includeAuthor, bool includePostedAt,
        out string? header)
    {
        var headerElements = new List<string>(2);

        if (includeAuthor)
        {
            headerElements.Add($"von {postedText.Author}");
        }

        if (includePostedAt)
        {
            headerElements.Add($"am {postedText.PostedAt:dd.MM.yyyy}");
        }

        if (headerElements.Count > 0)
        {
            header = CapitalizeFirstChar(string.Join(" ", headerElements));
            return true;
        }

        header = default;
        return false;
    }

    private static bool CreateHeaderLine(PostedText postedText, int currentNumber, int count, out string? header)
    {
        var headerElements = new List<string>(4);

        if (ExportConfiguration.IncludePageNumber && currentNumber % 10 == 1)
        {
            headerElements.Add($"Seite {currentNumber / 10 + 1}");
        }

        if (ExportConfiguration.IncludePostNumber)
        {
            headerElements.Add($"({currentNumber}/{count})");
        }

        if (ExportConfiguration.IncludePostAuthor)
        {
            headerElements.Add($"von {postedText.Author}");
        }

        if (ExportConfiguration.IncludePostPostedAt)
        {
            headerElements.Add($"am {postedText.PostedAt:dd.MM.yyyy HH:mm}");
        }

        if (headerElements.Count > 0)
        {
            header = CapitalizeFirstChar(string.Join(" ", headerElements));
            return true;
        }

        header = default;
        return false;
    }

    private static string CapitalizeFirstChar(string text)
    {
        return string.Concat(text[0].ToString().ToUpper(), text.AsSpan(1));
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