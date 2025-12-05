using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Biorxiv
{
    public class BiorxivParser : IParseIndexerResponse
    {
        public IBiorxivSettings Settings { get; set; }
        public string Server { get; set; }
        public string ContentBaseUrl { get; set; }
        public string SourceName { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return releases;
            }

            if (indexerResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            JObject json;

            try
            {
                json = JObject.Parse(indexerResponse.Content);
            }
            catch (Exception ex)
            {
                throw new IndexerException(indexerResponse, "Failed to parse {0} response: {1}", SourceName, ex.Message);
            }

            var collection = json["collection"] as JArray;
            if (collection == null || collection.Count == 0)
            {
                return releases;
            }

            var latestEntries = collection
                .Select(c => new
                {
                    Token = c,
                    Doi = DoiUtility.Normalize(c.Value<string>("doi"))
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.Doi) && !string.Equals(c.Token.Value<string>("type"), "withdrawn", StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c.Doi)
                .Select(g => g.OrderByDescending(c => ParseVersion(c.Token)).FirstOrDefault())
                .Where(c => c != null);

            foreach (var entry in latestEntries)
            {
                try
                {
                    var doi = entry.Doi;
                    var item = entry.Token;
                    if (string.IsNullOrWhiteSpace(doi))
                    {
                        continue;
                    }

                    var version = ParseVersion(item);
                    var versionSuffix = $"v{version}";
                    var title = item.Value<string>("title") ?? "Unknown Title";
                    var author = GetFirstAuthor(item.Value<string>("authors"));
                    var publishDate = item.Value<string>("date");

                    // Biorxiv/Medrxiv are preprint servers, but may have published_in field if published
                    var journal = item.Value<string>("published_in");

                    var release = new ReleaseInfo
                    {
                        Guid = $"{SourceName}-{doi}-{versionSuffix}",
                        Title = $"{author} - {title}",
                        Book = title,
                        Author = author,
                        Doi = doi,
                        Source = journal, // Store journal name in Source field (if published)
                        DownloadUrl = $"{ContentBaseUrl}/content/{doi}{versionSuffix}.full.pdf",
                        InfoUrl = $"{ContentBaseUrl}/content/{doi}{versionSuffix}",
                        Container = "PDF",
                        Categories = new List<int> { 8000 },
                        Size = 0,
                        DownloadProtocol = DownloadProtocol.Http
                    };

                    if (DateTime.TryParse(publishDate, out var publish))
                    {
                        release.PublishDate = publish;
                    }
                    else
                    {
                        release.PublishDate = DateTime.UtcNow;
                    }

                    releases.Add(release);
                }
                catch (Exception)
                {
                    // Ignore malformed entries
                }
            }

            return releases;
        }

        private static int ParseVersion(JToken item)
        {
            var versionString = item?.Value<string>("version");

            return int.TryParse(versionString, out var version) ? version : 1;
        }

        private static string GetFirstAuthor(string authors)
        {
            if (string.IsNullOrWhiteSpace(authors))
            {
                return "Unknown Author";
            }

            var first = authors.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(first) ? "Unknown Author" : first;
        }
    }
}
