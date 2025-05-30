﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using PostExporter.Utils;

namespace PostExporter.Scrape;

public static class Scraper
{
    private static readonly HashSet<HttpStatusCode> IgnoredStatusCodes =
    [
        HttpStatusCode.Unauthorized, HttpStatusCode.PaymentRequired, HttpStatusCode.Forbidden, HttpStatusCode.NotFound,
        HttpStatusCode.InternalServerError, HttpStatusCode.NotImplemented, HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
    ];

    private static readonly Regex UserAndDateTimeRegex = new(@"^von\s.+\sam\s\d{2}.\d{2}.\d{4}\s\d{2}:\d{2}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UserAndDateRegex = new(@"^von\s.+\sam\s\d{2}.\d{2}.\d{4}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HttpClient? _client;
    public static string? BaseUrl { get; set; }

    private static HttpClient GetOrCreateHttpClient(bool createNew = false)
    {
        if (_client == null || createNew)
        {
            var cookieContainer = new CookieContainer();
            var clientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseDefaultCredentials = true,
                CookieContainer = cookieContainer,
                UseCookies = true
            };
            _client = new HttpClient(clientHandler);
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:105.0) Gecko/20100101 Firefox/105.0");
        }

        return _client;
    }

    public static async Task<(bool, string)> DownloadImage(string uriString, string directoryPath, string fileName)
    {
        if (!TryCreateUri(uriString, out var uri))
        {
            return (false, string.Empty);
        }

        if (uri == null)
        {
            return (false, string.Empty);
        }

        var uriWithoutQuery = uri.GetLeftPart(UriPartial.Path);
        var fileExtension = Path.GetExtension(uriWithoutQuery);
        if (string.IsNullOrEmpty(fileExtension))
        {
            return (false, string.Empty);
        }

        var fileNameWithExtension = $"{fileName}{fileExtension}";
        var path = Path.Combine(directoryPath, fileNameWithExtension);

        byte[] imageBytes;
        try
        {
            imageBytes = await GetOrCreateHttpClient().GetByteArrayAsync(uri);
        }
        catch (HttpRequestException e)
        {
            if (!e.StatusCode.HasValue || !IgnoredStatusCodes.Contains(e.StatusCode.Value))
            {
                SentryUtil.HandleMessage($"{e} for \"{uriString}\"");
            }

            return (false, string.Empty);
        }

        await File.WriteAllBytesAsync(path, imageBytes);

        return (true, fileNameWithExtension);
    }

    public static async Task<bool> TestImage(string uriString)
    {
        if (!TryCreateUri(uriString, out var uri))
        {
            return false;
        }

        if (uri == null)
        {
            return false;
        }

        try
        {
            var imageBytes = await GetOrCreateHttpClient().GetByteArrayAsync(uri);
            return imageBytes.Length > 0;
        }
        catch (HttpRequestException e)
        {
            SentryUtil.HandleMessage($"{e} for \"{uriString}\"");

            return false;
        }
    }

    private static bool TryCreateUri(string uriString, out Uri? uri)
    {
        // Relative URI
        if (uriString.StartsWith('/'))
        {
            uriString = $"{BaseUrl}{uriString}";
        }

        if (Uri.TryCreate(uriString, UriKind.Absolute, out uri))
        {
            return true;
        }

        SentryUtil.HandleMessage($"Cannot parse URI \"{uriString}\"");

        return false;
    }

    public static async Task<List<Post>> GetPosts(Thread thread, CancellationToken cancellationToken)
    {
        return await GetPosts(thread, thread.Url, cancellationToken);
    }

    private static async Task<List<Post>> GetPosts(Thread thread, string threadUrl, CancellationToken cancellationToken)
    {
        var threadResponse = await GetOrCreateHttpClient().GetAsync(threadUrl, cancellationToken);
        threadResponse.EnsureSuccessStatusCode();
        var threadContent = await threadResponse.Content.ReadAsStringAsync(cancellationToken);

        var doc = new HtmlDocument
        {
            OptionEmptyCollection = true
        };
        doc.LoadHtml(threadContent);

        var postRows =
            doc.DocumentNode.SelectNodes("//div[@class='forum_postbody']/div[@class='forum_postbody_content']");

        var posts = new List<Post>();

        foreach (var postRow in postRows)
        {
            var foundHeading = false;
            HtmlNode? infoNode = null;
            var textNodes = new List<HtmlNode>();

            foreach (var childNode in postRow.ChildNodes)
            {
                if (!foundHeading)
                {
                    switch (childNode.Name)
                    {
                        case "#text" or "span":
                            continue;
                        case "h2":
                            foundHeading = true;
                            break;
                        default:
                            throw new NotSupportedException(
                                $"Heading node is {childNode.Name} with classes {string.Join(", ", childNode.GetClasses())}");
                    }
                }
                else if (infoNode == null)
                {
                    switch (childNode.Name)
                    {
                        case "#text":
                            continue;
                        case "span" when childNode.HasClass("forum_postbody_small"):
                            infoNode = childNode;
                            break;
                        default:
                            throw new NotSupportedException(
                                $"Info node is {childNode.Name} with classes {string.Join(", ", childNode.GetClasses())}");
                    }
                }
                else if (childNode.HasClass("signature"))
                {
                    break;
                }
                else
                {
                    textNodes.Add(childNode);
                }
            }

            if (!foundHeading)
            {
                throw new NotSupportedException("Row without heading node");
            }

            if (infoNode == null)
            {
                throw new NotSupportedException("Row without info node");
            }

            if (textNodes.Count == 0)
            {
                throw new NotSupportedException("Row without text nodes");
            }

            var head = HttpUtility.HtmlDecode(infoNode.InnerText).Trim();
            var (author, postedAt) = GetUserAndDateTime(head);

            posts.Add(new Post(author, postedAt, textNodes, thread));
        }

        var nextPostsButton = doc.DocumentNode.SelectSingleNode("//div[@class='pagesBar']/a[position() = (last()-1)]");

        if (nextPostsButton is not { InnerHtml: "&raquo;" })
        {
            return posts;
        }

        var nextPostsPath = nextPostsButton.Attributes["href"].Value;
        nextPostsPath = nextPostsPath.Replace("amp;", "");
        posts.AddRange(await GetPosts(thread, $"{BaseUrl}/forum/{nextPostsPath}", cancellationToken));

        return posts;
    }

    public static async Task<(List<Thread>, bool)> GetThreads(Group group)
    {
        var threadsPath = await GetThreadsPath(group.Url);
        if (threadsPath == null)
        {
            SentryUtil.HandleMessage($"No threads path for group URL \"{group.Url}\"");
            return ([], false);
        }

        return await GetThreads(group, $"{BaseUrl}{threadsPath}");
    }

    private static async Task<(List<Thread>, bool)> GetThreads(Group group, string threadsUrl)
    {
        var threadsResponse = await GetOrCreateHttpClient().GetAsync(threadsUrl);
        threadsResponse.EnsureSuccessStatusCode();
        var threadsContent = await threadsResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument
        {
            OptionEmptyCollection = true
        };
        doc.LoadHtml(threadsContent);

        var threadRows = doc.DocumentNode.SelectNodes("//table[@class='sortable tableForums']/tbody/tr");
        var loadedAllThreadsSuccessfully = true;

        var threads = new List<Thread>();
        foreach (var threadRow in threadRows)
        {
            var titleNode =
                threadRow.SelectSingleNode(
                    "./td[1]/h2[@class='thread' or @class='newthread' or @class='sticky' or @class='newsticky']/a");

            if (titleNode == null)
            {
                loadedAllThreadsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find thread title in \"{threadRow.InnerHtml}\"");

                continue;
            }

            var title = HttpUtility.HtmlDecode(titleNode.InnerText).Trim();
            var path = titleNode.Attributes["href"].Value;

            var detailsNode = threadRow.SelectSingleNode("./td[1]/span[@class='small']");

            if (detailsNode == null)
            {
                loadedAllThreadsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find thread details in \"{threadRow.InnerHtml}\"");

                continue;
            }

            var details = HttpUtility.HtmlDecode(detailsNode.InnerText).Trim();
            var (author, postedAt) = GetUserAndDateTime(details);

            var id = GetThreadIdFromPath(path);
            threads.Add(new Thread(id, author, postedAt, title, $"{BaseUrl}/forum/{path}", group));
        }

        var nextThreadsButton =
            doc.DocumentNode.SelectSingleNode("//div[@id='content']/form/p/span[1]/a[position() = (last()-1)]");

        if (nextThreadsButton is not { InnerHtml: "&raquo;" })
        {
            return (threads, loadedAllThreadsSuccessfully);
        }

        var nextThreadsPath = nextThreadsButton.Attributes["href"].Value;
        nextThreadsPath = nextThreadsPath.Replace("amp;", "");

        var (additionalThreads, loadedAdditionalThreadsSuccessfully) =
            await GetThreads(group, $"{BaseUrl}/forum/{nextThreadsPath}");
        threads.AddRange(additionalThreads);

        return (threads, loadedAllThreadsSuccessfully && loadedAdditionalThreadsSuccessfully);
    }

    private static int GetThreadIdFromPath(string path)
    {
        var split = path.Split("-");

        return int.Parse(split[^2]);
    }

    private static async Task<string?> GetThreadsPath(string groupUrl)
    {
        var groupResponse = await GetOrCreateHttpClient().GetAsync(groupUrl);
        groupResponse.EnsureSuccessStatusCode();
        var groupContent = await groupResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument
        {
            OptionEmptyCollection = true
        };
        doc.LoadHtml(groupContent);

        var node = doc.DocumentNode.SelectSingleNode("//a[@class='icon topics']");

        if (node == null)
        {
            SentryUtil.HandleMessage(
                $"Could not find threads button in \"{doc.DocumentNode.SelectSingleNode("//div[@id='content']")?.InnerHtml}\"");
            return null;
        }

        return node.Attributes["href"].Value;
    }

    public static async Task<(List<Group>, bool)> GetGroups()
    {
        return await GetGroups("mygroups.html");
    }

    private static async Task<(List<Group>, bool)> GetGroups(string groupsPath)
    {
        var groupsResponse = await GetOrCreateHttpClient().GetAsync($"{BaseUrl}/groups/{groupsPath}");
        groupsResponse.EnsureSuccessStatusCode();
        var groupsContent = await groupsResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument
        {
            OptionEmptyCollection = true
        };
        doc.LoadHtml(groupsContent);

        var groupRows =
            doc.DocumentNode.SelectNodes("//div[@id='tab-pane-1']/div[@class='tab-page']/table[@class='table']/tr");

        if (groupRows.Count == 0)
        {
            SentryUtil.HandleMessage(
                $"Could not find group table nodes in \"{doc.DocumentNode.SelectSingleNode("//div[@id='tab-pane-1']")?.InnerHtml}\"");
            return ([], false);
        }

        // Remove header row
        groupRows.Remove(0);

        var groups = new List<Group>();
        var loadedAllGroupsSuccessfully = true;

        foreach (var groupRow in groupRows)
        {
            var tableNode = groupRow.SelectSingleNode(".//table[@class='profileTable']/tbody");

            if (tableNode == null)
            {
                loadedAllGroupsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find table node in \"{groupRow.InnerHtml}\"");

                continue;
            }

            var titleNode = tableNode.SelectSingleNode("./tr[1]/td[2]/a");

            if (titleNode == null)
            {
                loadedAllGroupsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find title in \"{groupRow.InnerHtml}\"");

                continue;
            }

            var title = HttpUtility.HtmlDecode(titleNode.InnerText).Trim();
            var pathWithQuery = titleNode.Attributes["href"].Value;

            var founderNode = tableNode.SelectSingleNode("./tr[3]/td[2]/a");

            if (founderNode == null)
            {
                loadedAllGroupsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find founder in \"{groupRow.InnerHtml}\"");

                continue;
            }

            var founder = HttpUtility.HtmlDecode(founderNode.InnerText).Trim();

            var foundedAtNode = tableNode.SelectSingleNode("./tr[4]/td[2]");

            if (foundedAtNode == null)
            {
                loadedAllGroupsSuccessfully = false;
                SentryUtil.HandleMessage($"Could not find founded at in \"{groupRow.InnerHtml}\"");

                continue;
            }

            var foundedAt = DateTime.ParseExact(HttpUtility.HtmlDecode(foundedAtNode.InnerText), "dd.MM.yyyy",
                CultureInfo.InvariantCulture);

            var id = GetGroupIdFromPath(pathWithQuery);
            groups.Add(new Group(id, founder, foundedAt, title, $"{BaseUrl}{pathWithQuery}"));
        }

        var nextGroupsButton = doc.DocumentNode.SelectSingleNode(
            "//div[@id='tab-pane-1']/div[@class='tab-page']/div[@class='pagesBar']/span/a[position() = (last()-1)]");

        if (nextGroupsButton is not { InnerHtml: "&raquo;" })
        {
            return (groups, loadedAllGroupsSuccessfully);
        }

        var nextGroupsPath = nextGroupsButton.Attributes["href"].Value;
        nextGroupsPath = nextGroupsPath.Replace("amp;", "");

        var (additionalGroups, loadedAdditionalGroupsSuccessfully) = await GetGroups(nextGroupsPath);
        groups.AddRange(additionalGroups);

        return (groups, loadedAllGroupsSuccessfully && loadedAdditionalGroupsSuccessfully);
    }

    private static int GetGroupIdFromPath(string pathWithQuery)
    {
        var query = pathWithQuery[(pathWithQuery.IndexOf('?') + 1)..];
        var queryParts = query.Split("&");

        foreach (var queryPart in queryParts)
        {
            if (queryPart.StartsWith("id="))
            {
                return int.Parse(queryPart[(queryPart.IndexOf('=') + 1)..]);
            }
        }

        throw new ArgumentException($"No group ID in \"{pathWithQuery}\"");
    }

    public static async Task<bool> Login(string username, string password)
    {
        var authContent = new FormUrlEncodedContent(
            new List<KeyValuePair<string, string>>
            {
                new("username", username),
                new("password", password),
                new("use_cookie", "1")
            }
        );

        var loginResponse = await GetOrCreateHttpClient(true).PostAsync($"{BaseUrl}/login.do", authContent);
        loginResponse.EnsureSuccessStatusCode();
        var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();

        return !loginResponseBody.Contains("Die Zugangsdaten sind nicht richtig");
    }

    private static (string, DateTime) GetUserAndDateTime(string text)
    {
        if (UserAndDateTimeRegex.IsMatch(text))
        {
            var split = text.Split(" am ");
            var user = split[0][4..];
            var dateTime = DateTime.ParseExact(split[1], "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

            return (user, dateTime);
        }

        if (UserAndDateRegex.IsMatch(text))
        {
            var split = text.Split(" am ");
            var user = split[0][4..];
            var dateTime = DateTime.ParseExact(split[1], "dd.MM.yyyy", CultureInfo.InvariantCulture);

            return (user, dateTime);
        }

        throw new ArgumentException($"Cannot match user and date (time) from \"{text}\"");
    }
}