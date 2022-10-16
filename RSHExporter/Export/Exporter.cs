using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using RSHExporter.Scrape;

namespace RSHExporter.Export;

public static class Exporter
{
    private static readonly HashSet<string> SingleParagraphTags = new()
    {
        "a", "abbr", "acronym", "b", "bdi", "bdo", "big", "cite", "code", "data", "del", "dfn", "em", "i", "ins", "kbd",
        "label", "mark", "q", "s", "samp", "small", "span", "strong", "sub", "sup", "time", "u", "var", "wbr"
    };

    public static async Task Export(List<Post> posts)
    {
        var thread = posts[0].Thread;
        var group = thread.Group;

        var (directoryPath, imagesDirectoryPath, imagesDirectory, filePath) = CreatePaths(group, thread);

        Directory.CreateDirectory(directoryPath);

        if (ExportConfiguration.IncludeImages)
        {
            if (ExportConfiguration.DownloadImages)
            {
                var directoryAlreadyCreated = false;

                for (int i = 0; i < posts.Count; ++i)
                {
                    var post = posts[i];

                    var imageTags = post.Node.SelectNodes(".//img");
                    if (imageTags == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < imageTags.Count; ++j)
                    {
                        var imageTag = imageTags[j];

                        var source = imageTag.Attributes["src"].Value;
                        var altName = imageTag.Attributes["alt"].Value;

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
                            imageTag.Attributes["src"].Value = Path.Combine(imagesDirectory, fileNameWithExtension);
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var post in posts)
            {
                var imageTags = post.Node.SelectNodes(".//img");
                if (imageTags == null)
                {
                    continue;
                }

                foreach (var imageTag in imageTags)
                {
                    imageTag.Remove();
                }
            }
        }

        switch (ExportConfiguration.FileFormat)
        {
            case FileFormat.Txt:
            {
                await using var txtFile = new StreamWriter(filePath);

                if (ExportConfiguration.IncludeGroup)
                {
                    await txtFile.WriteLineAsync(group.Title);

                    if (CreateHeaderLine(group, ExportConfiguration.IncludeGroupAuthor,
                            ExportConfiguration.IncludeGroupPostedAt, out var header))
                    {
                        await txtFile.WriteLineAsync(header);
                    }

                    if (ExportConfiguration.IncludeGroupUrl)
                    {
                        await txtFile.WriteLineAsync($"URL: {group.Url}");
                    }

                    await txtFile.WriteLineAsync();
                }

                if (ExportConfiguration.IncludeThread)
                {
                    await txtFile.WriteLineAsync(thread.Title);

                    if (CreateHeaderLine(thread, ExportConfiguration.IncludeThreadAuthor,
                            ExportConfiguration.IncludeThreadPostedAt, out var header))
                    {
                        await txtFile.WriteLineAsync(header);
                    }

                    if (ExportConfiguration.IncludeThreadUrl)
                    {
                        await txtFile.WriteLineAsync($"URL: {thread.Url}");
                    }

                    await txtFile.WriteLineAsync();
                }

                for (var i = 0; i < posts.Count; ++i)
                {
                    var post = posts[ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i];

                    if (CreateHeaderLine(post, i + 1, posts.Count, out var header))
                    {
                        await txtFile.WriteLineAsync(header);
                    }

                    await WriteLines(txtFile, post.Node);
                    await txtFile.WriteLineAsync();
                }

                break;
            }

            case FileFormat.Html:
            {
                await using var htmlFile = new StreamWriter(filePath);

                await htmlFile.WriteLineAsync("<html>");
                await htmlFile.WriteLineAsync($"<head><title>{HttpUtility.HtmlEncode(thread.Title)}</title></head>");
                await htmlFile.WriteLineAsync("<body>");

                if (ExportConfiguration.IncludeGroup)
                {
                    await htmlFile.WriteLineAsync($"<h3>{HttpUtility.HtmlEncode(group.Title)}</h3>");

                    if (CreateHeaderLine(group, ExportConfiguration.IncludeGroupAuthor,
                            ExportConfiguration.IncludeGroupPostedAt, out var header))
                    {
                        await htmlFile.WriteLineAsync($"<p>{HttpUtility.HtmlEncode(header)}</p>");
                    }

                    if (ExportConfiguration.IncludeGroupUrl)
                    {
                        await htmlFile.WriteLineAsync($"<p>URL: <a href=\"{group.Url}\">{group.Url}</a></p>");
                    }
                }

                if (ExportConfiguration.IncludeThread)
                {
                    await htmlFile.WriteLineAsync($"<h2>{HttpUtility.HtmlEncode(thread.Title)}</h2>");

                    if (CreateHeaderLine(thread, ExportConfiguration.IncludeThreadAuthor,
                            ExportConfiguration.IncludeThreadPostedAt, out var header))
                    {
                        await htmlFile.WriteLineAsync($"<p>{HttpUtility.HtmlEncode(header)}</p>");
                    }

                    if (ExportConfiguration.IncludeThreadUrl)
                    {
                        await htmlFile.WriteLineAsync($"<p>URL: <a href=\"{thread.Url}\">{thread.Url}</a></p>");
                    }
                }

                for (var i = 0; i < posts.Count; ++i)
                {
                    var post = posts[ExportConfiguration.ReserveOrder ? posts.Count - 1 - i : i];

                    await htmlFile.WriteLineAsync("<div>");

                    if (CreateHeaderLine(post, i + 1, posts.Count, out var header))
                    {
                        await htmlFile.WriteLineAsync($"<p><strong>{HttpUtility.HtmlEncode(header)}</strong></p>");
                    }

                    await htmlFile.WriteLineAsync($"{post.Node.InnerHtml}");
                    await htmlFile.WriteLineAsync("</div><br/>");
                }

                await htmlFile.WriteLineAsync("</body>");
                await htmlFile.WriteLineAsync("</html>");

                break;
            }

            case FileFormat.Docx:
            {
                // TODO
                // using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
                // var mainPart = document.AddMainDocumentPart();
                // mainPart.Document.Save();

                // await File.WriteAllBytesAsync(filePath, generatedDocument.ToArray());

                break;
            }

            default:
                throw new NotSupportedException(ExportConfiguration.FileFormat.ToString());
        }
    }

    private static async Task WriteLines(StreamWriter txtFile, HtmlNode node)
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

                await txtFile.WriteLineAsync(text);
            }
            else
            {
                var text = node.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await txtFile.WriteLineAsync(HttpUtility.HtmlDecode(text));
                }
            }
        }
        else
        {
            foreach (var child in node.ChildNodes)
            {
                await WriteLines(txtFile, child);
            }
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

        fileName.Append('.').Append(ExportConfiguration.FileFormat.FileExtension());

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