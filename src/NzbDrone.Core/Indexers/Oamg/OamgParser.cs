using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Oamg
{
    public class OamgParser : IParseIndexerResponse
    {
        public OamgSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            // OpenAlex/OA.mg search returns { results: [...] }
            JToken json;
            try
            {
                json = JToken.Parse(indexerResponse.Content);
            }
            catch
            {
                throw new IndexerException(indexerResponse, "Failed to parse JSON response");
            }

            var results = json["results"] ?? json;

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
                    var downloadUrl = item["open_access"]?["oa_url"]?.ToString() ??
                                      item["best_oa_location"]?["url"]?.ToString() ??
                                      item["open_access"]?["oa_url_pdf"]?.ToString() ??
                                      item["primary_location"]?["source"]?["url"]?.ToString();

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        continue;
                    }

                    var authors = item["authorships"] as JArray;
                    string author = null;
                    if (authors != null)
                    {
                         author = authors.FirstOrDefault()?["author"]?["display_name"]?.ToString();
                    }

                    var doi = DoiUtility.Normalize(item["doi"]?.ToString() ?? item["ids"]?["doi"]?.ToString());

                    // Extract journal name from primary_location.source.display_name (OpenAlex structure)
                    var journal = item["primary_location"]?["source"]?["display_name"]?.ToString() ??
                                  item["raw_source_name"]?.ToString();

                    var release = new ReleaseInfo();
                    release.Guid = $"Oamg-{id}";
                    release.Title = $"{author} - {title}";
                    release.Book = title;
                    release.Author = author;
                    release.Doi = doi;
                    release.Source = journal; // Store journal name in Source field
                    release.DownloadUrl = downloadUrl;
                    release.InfoUrl = $"https://oa.mg/work/{id}";
                    release.Size = 0;

                    var publishedDate = item["publication_date"]?.ToString();
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
