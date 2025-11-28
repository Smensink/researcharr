using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Core
{
    public class CoreParser : IParseIndexerResponse
    {
        public CoreSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            var json = JObject.Parse(indexerResponse.Content);
            var results = json["results"];

            if (results == null)
            {
                return releases;
            }

            foreach (var item in results)
            {
                try
                {
                    var id = item["id"]?.ToString();
                    var title = item["title"]?.ToString();
                    var downloadUrl = item["downloadUrl"]?.ToString();

                    // Check for full text link if downloadUrl is missing
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        // Fallback: check links array or fullTextIdentifier
                        // This depends on exact API response structure which varies by version
                        // V3 usually has downloadUrl or links
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        continue;
                    }

                    var authors = item["authors"] as JArray;
                    var author = authors?.FirstOrDefault()?["name"]?.ToString() ?? "Unknown Author";

                    var release = new ReleaseInfo();
                    release.Guid = $"Core-{id}";
                    release.Title = $"{author} - {title}";
                    release.DownloadUrl = downloadUrl;
                    release.InfoUrl = $"https://core.ac.uk/display/{id}";
                    release.Size = 0;

                    var publishedDate = item["publishedDate"]?.ToString();
                    if (DateTime.TryParse(publishedDate, out var pubDate))
                    {
                        release.PublishDate = pubDate;
                    }
                    else
                    {
                        release.PublishDate = DateTime.UtcNow;
                    }

                    release.DownloadProtocol = DownloadProtocol.Http;
                    releases.Add(release);
                }
                catch (Exception)
                {
                    // Ignore malformed entries
                }
            }

            return releases;
        }
    }
}
