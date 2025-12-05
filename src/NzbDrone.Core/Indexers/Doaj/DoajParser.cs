using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Doaj
{
    public class DoajParser : IParseIndexerResponse
    {
        public DoajSettings Settings { get; set; }

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
                    var bibjson = item["bibjson"];
                    if (bibjson == null)
                    {
                        continue;
                    }

                    var id = item["id"]?.ToString();
                    var title = bibjson["title"]?.ToString() ?? "Unknown DOAJ Article";
                    var links = bibjson["link"] as JArray;
                    var downloadUrl = links?
                        .FirstOrDefault(l =>
                            string.Equals(l["type"]?.ToString(), "fulltext", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(l["type"]?.ToString(), "pdf", StringComparison.OrdinalIgnoreCase))?["url"]?.ToString()
                        ?? links?.FirstOrDefault()?["url"]?.ToString();

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        continue;
                    }

                    var authors = bibjson["author"] as JArray;
                    var author = authors?.FirstOrDefault()?["name"]?.ToString() ?? "Unknown Author";
                    var guid = string.IsNullOrWhiteSpace(id) ? $"Doaj-{Guid.NewGuid()}" : $"Doaj-{id}";

                    var identifiers = bibjson["identifier"] as JArray;
                    var doi = identifiers?.FirstOrDefault(i => string.Equals(i["type"]?.ToString(), "doi", StringComparison.OrdinalIgnoreCase))?["id"]?.ToString();
                    doi = DoiUtility.Normalize(doi);

                    // Extract journal name from bibjson.journal or bibjson.publisher
                    var journal = bibjson["journal"]?["title"]?.ToString() ??
                                   bibjson["journal"]?["name"]?.ToString() ??
                                   bibjson["publisher"]?.ToString();

                    if (downloadUrl.StartsWith("//"))
                    {
                        downloadUrl = "https:" + downloadUrl;
                    }
                    else if (downloadUrl.StartsWith("/"))
                    {
                        downloadUrl = $"https://doaj.org{downloadUrl}";
                    }

                    var release = new ReleaseInfo();
                    release.Guid = guid;
                    release.Title = $"{author} - {title}";
                    release.Book = title;
                    release.Author = author;
                    release.Doi = doi;
                    release.Source = journal; // Store journal name in Source field
                    release.DownloadUrl = downloadUrl;
                    release.InfoUrl = string.IsNullOrWhiteSpace(id) ? downloadUrl : $"https://doaj.org/article/{id}";
                    release.Size = 0;

                    var createdDate = item["created_date"]?.ToString();
                    if (DateTime.TryParse(createdDate, out var pubDate))
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
