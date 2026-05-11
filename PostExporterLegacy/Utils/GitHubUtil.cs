using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace PostExporter.Utils;

public static class GitHubUtil
{
    private const string LatestReleaseUrl =
        @"https://api.github.com/repos/maximilian-hammerl/post-exporter/releases/latest";

    private static HttpClient? _client;

    private static HttpClient GetOrCreateHttpClient()
    {
        if (_client == null)
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"maximilian-hammerl post-exporter ({Util.GetAppName()}@{Util.GetCurrentVersionAsString(4)})");
        }

        return _client;
    }

    public static async Task<Release> GetLatestRelease()
    {
        var response = await GetOrCreateHttpClient().GetAsync(LatestReleaseUrl);
        var responseContent = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<Release>(responseContent) ??
               throw new InvalidOperationException("Cannot deserialize latest release");
    }

    public class Release
    {
        private string? _downloadUrl;

        private Version? _version;
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }

        [JsonPropertyName("assets")] public List<Asset>? Assets { get; set; }

        public Version GetVersion()
        {
            if (_version == null)
            {
                if (TagName == null)
                {
                    throw new InvalidOperationException("No tag name");
                }

                _version = Version.Parse(TagName[1..]);
            }

            return _version;
        }

        public string GetDownloadUrl()
        {
            if (_downloadUrl == null)
            {
                if (Assets == null || Assets.Count == 0)
                {
                    throw new InvalidOperationException("No assets");
                }

                if (Assets.Count == 1)
                {
                    _downloadUrl = Assets[0].BrowserDownloadUrl;
                }
                else
                {
                    foreach (var asset in Assets)
                    {
                        if (asset.Name == null || !asset.Name.EndsWith(".exe"))
                        {
                            continue;
                        }

                        _downloadUrl = asset.BrowserDownloadUrl;
                        break;
                    }
                }

                if (_downloadUrl == null)
                {
                    throw new InvalidOperationException("No asset for executable");
                }
            }

            return _downloadUrl;
        }
    }

    [UsedImplicitly]
    public class Asset
    {
        [UsedImplicitly]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [UsedImplicitly]
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}