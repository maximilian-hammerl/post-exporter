using System;
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
using Sentry;

namespace RSHExporter.Scrape;

public static class Scraper
{
    private static readonly Regex UserAndDateTimeRegex = new(@"^von\s.+\sam\s\d{2}.\d{2}.\d{4}\s\d{2}:\d{2}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static HttpClient? _client;

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
                UseCookies = true,
            };
            _client = new HttpClient(clientHandler);
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:105.0) Gecko/20100101 Firefox/105.0");
        }

        return _client;
    }

    public static async Task<(bool, string)> DownloadImage(string uriString, string directoryPath, string fileName)
    {
        // Relative URI
        if (uriString.StartsWith("/"))
        {
            uriString = $"https://rollenspielhimmel.de{uriString}";
        }

        Uri uri;
        try
        {
            uri = new Uri(uriString);
        }
        catch (UriFormatException)
        {
            if (LoginPage.CollectDataAccepted)
            {
                SentrySdk.CaptureMessage($"Could not determine format of URI \"{uriString}\"!");
            }

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

        var imageBytes = await GetOrCreateHttpClient().GetByteArrayAsync(uri);
        await File.WriteAllBytesAsync(path, imageBytes);

        return (true, fileNameWithExtension);
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

        var doc = new HtmlDocument();
        doc.LoadHtml(threadContent);

        var postRows =
            doc.DocumentNode.SelectNodes("//div[@class='forum_postbody']/div[@class='forum_postbody_content']");

        var posts = new List<Post>();

        foreach (var postRow in postRows)
        {
            var head = HttpUtility.HtmlDecode(postRow.SelectSingleNode("./span[@class='forum_postbody_small']")
                .InnerText.Trim());
            var (author, postedAt) = GetUserAndDateTime(head);

            var bodyPTag = postRow.SelectSingleNode("./p[1]");

            posts.Add(new Post(author, postedAt, bodyPTag, thread));
        }

        var nextPostsButton = doc.DocumentNode.SelectSingleNode("//div[@class='pagesBar']/a[position() = (last()-1)]");

        if (nextPostsButton is not { InnerHtml: "&raquo;" })
        {
            return posts;
        }

        var nextPostsPath = nextPostsButton.Attributes["href"].Value;
        nextPostsPath = nextPostsPath.Replace("amp;", "");
        posts.AddRange(await GetPosts(thread, $"https://rollenspielhimmel.de/forum/{nextPostsPath}",
            cancellationToken));

        return posts;
    }

    public static async Task<List<Thread>> GetThreads(Group group)
    {
        var threadsPath = await GetThreadsPath(group.Url);

        var threadsResponse = await GetOrCreateHttpClient().GetAsync($"https://rollenspielhimmel.de{threadsPath}");
        threadsResponse.EnsureSuccessStatusCode();
        var threadsContent = await threadsResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(threadsContent);

        var threadRows = doc.DocumentNode.SelectNodes("//table[@class='sortable tableForums']/tbody/tr");

        if (threadRows == null)
        {
            return new List<Thread>();
        }

        var threads = new List<Thread>();
        foreach (var threadRow in threadRows)
        {
            var titleATag = threadRow.SelectSingleNode("./td[1]/h2[@class='thread' or @class='sticky']/a");
            var title = HttpUtility.HtmlDecode(titleATag.InnerText);
            var path = titleATag.Attributes["href"].Value;

            var details = HttpUtility.HtmlDecode(threadRow.SelectSingleNode("./td[1]/span[@class='small']").InnerText);
            var (author, postedAt) = GetUserAndDateTime(details);

            threads.Add(new Thread(author, postedAt, title, $"https://rollenspielhimmel.de/forum/{path}", group));
        }

        return threads;
    }

    private static async Task<string> GetThreadsPath(string groupUrl)
    {
        var groupResponse = await GetOrCreateHttpClient().GetAsync(groupUrl);
        groupResponse.EnsureSuccessStatusCode();
        var groupContent = await groupResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(groupContent);

        return doc.DocumentNode.SelectSingleNode("//a[@class='icon topics']").Attributes["href"].Value;
    }

    private static async Task<List<Group>> GetGroups()
    {
        return await GetGroups("mygroups.html");
    }

    private static async Task<List<Group>> GetGroups(string groupsPath)
    {
        var groupsResponse =
            await GetOrCreateHttpClient().GetAsync($"https://rollenspielhimmel.de/groups/{groupsPath}");
        groupsResponse.EnsureSuccessStatusCode();
        var groupsContent = await groupsResponse.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(groupsContent);

        var groupRows =
            doc.DocumentNode.SelectNodes("//div[@id='tab-pane-1']/div[@class='tab-page']/table[@class='table']/tr");

        // Remove header row
        groupRows.Remove(0);

        var groups = new List<Group>();

        foreach (var groupRow in groupRows)
        {
            var groupTbodyTag = groupRow.SelectSingleNode(".//table[@class='profileTable']/tbody");

            var titleATag = groupTbodyTag.SelectSingleNode("./tr[1]/td[2]/a");
            var title = HttpUtility.HtmlDecode(titleATag.InnerText);
            var path = titleATag.Attributes["href"].Value;

            var founderATag = groupTbodyTag.SelectSingleNode("./tr[3]/td[2]/a");
            var founder = HttpUtility.HtmlDecode(founderATag.InnerText);

            var foundedAtTdTag = groupTbodyTag.SelectSingleNode("./tr[4]/td[2]");
            var foundedAt = DateTime.ParseExact(HttpUtility.HtmlDecode(foundedAtTdTag.InnerText), "dd.MM.yyyy",
                CultureInfo.InvariantCulture);

            groups.Add(new Group(founder, foundedAt, title, $"https://rollenspielhimmel.de{path}"));
        }

        var nextGroupsButton = doc.DocumentNode.SelectSingleNode(
            "//div[@id='tab-pane-1']/div[@class='tab-page']/div[@class='pagesBar']/span/a[position() = (last()-1)]");

        if (nextGroupsButton is not { InnerHtml: "&raquo;" })
        {
            return groups;
        }

        var nextGroupsPath = nextGroupsButton.Attributes["href"].Value;
        nextGroupsPath = nextGroupsPath.Replace("amp;", "");
        groups.AddRange(await GetGroups(nextGroupsPath));

        return groups;
    }

    public static async Task<List<Group>?> LoginAndGetGroups(string username, string password)
    {
        var loginSuccessful = await Login(username, password);

        if (!loginSuccessful)
        {
            return null;
        }

        return await GetGroups();
    }

    private static async Task<bool> Login(string username, string password)
    {
        var authContent = new FormUrlEncodedContent(
            new List<KeyValuePair<string, string>>
            {
                new("username", username),
                new("password", password),
                new("use_cookie", "1"),
            }
        );

        var loginResponse =
            await GetOrCreateHttpClient(true).PostAsync("https://rollenspielhimmel.de/login.do", authContent);
        loginResponse.EnsureSuccessStatusCode();
        var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();

        return !loginResponseBody.Contains("Die Zugangsdaten sind nicht richtig");
    }

    private static (string, DateTime) GetUserAndDateTime(string text)
    {
        if (!UserAndDateTimeRegex.IsMatch(text))
        {
            throw new ArgumentException($"Cannot match user and date time from \"{text}\"");
        }

        var split = text.Split(" am ");
        var user = split[0][4..];
        var dateTime = DateTime.ParseExact(split[1], "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        return (user, dateTime);
    }
}