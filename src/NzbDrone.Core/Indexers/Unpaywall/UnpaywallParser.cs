using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Unpaywall
{
    public class UnpaywallParser : IParseIndexerResponse
    {
        public UnpaywallSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return releases;
            }

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            var json = JObject.Parse(indexerResponse.Content);
            var bestOaLocation = json["best_oa_location"];
            if (bestOaLocation == null || bestOaLocation.Type == JTokenType.Null)
            {
                return releases;
            }

            var url = bestOaLocation["url_for_pdf"]?.ToString() ?? bestOaLocation["url"]?.ToString();

            if (string.IsNullOrEmpty(url))
            {
                return releases;
            }

            var title = json["title"]?.ToString() ?? "Unknown Title";

            var authors = json["z_authors"] as JArray;
            var author = authors?.FirstOrDefault()?["raw_author_name"]?.ToString() ?? "Unknown Author";

            var doi = json["doi"]?.ToString() ?? Guid.NewGuid().ToString();

            var release = new ReleaseInfo();
            release.Guid = $"Unpaywall-{doi}";
            release.Title = $"{author} - {title}";
            release.Author = author;
            release.Book = title;
            release.Doi = doi;
            release.DownloadUrl = url;
            release.InfoUrl = $"https://doi.org/{doi}";
            release.Size = 0;

            var publishedDate = json["published_date"]?.ToString();
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

            return releases;
        }
    }
}
