using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Arxiv
{
    public class ArxivParser : IParseIndexerResponse
    {
        public ArxivSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            var xml = XDocument.Parse(indexerResponse.Content);
            var ns = xml.Root.GetDefaultNamespace();

            foreach (var entry in xml.Descendants(ns + "entry"))
            {
                try
                {
                    var id = entry.Element(ns + "id")?.Value;
                    var title = entry.Element(ns + "title")?.Value;
                    var summary = entry.Element(ns + "summary")?.Value;
                    var published = entry.Element(ns + "published")?.Value;

                    var authors = entry.Elements(ns + "author").Select(a => a.Element(ns + "name")?.Value).Where(n => n != null).ToList();
                    var author = authors.FirstOrDefault() ?? "Unknown Author";

                    // Extract DOI
                    XNamespace arxiv = "http://arxiv.org/schemas/atom";
                    var doi = DoiUtility.Normalize(entry.Element(arxiv + "doi")?.Value);

                    // Arxiv is a preprint server, so typically no journal
                    // However, if a paper has been published, journal info might be in comments
                    // For now, we'll leave Source as null since Arxiv doesn't provide journal info
                    string journal = null;

                    // Find PDF link
                    var pdfLink = entry.Elements(ns + "link")
                        .FirstOrDefault(l => (string)l.Attribute("title") == "pdf" || (string)l.Attribute("type") == "application/pdf")?
                        .Attribute("href")?.Value;

                    if (string.IsNullOrEmpty(pdfLink))
                    {
                        continue;
                    }

                    var release = new ReleaseInfo();
                    release.Guid = id ?? $"Arxiv-{Guid.NewGuid()}";
                    release.Title = $"{author} - {title}";
                    release.Book = title;
                    release.Author = author;
                    release.Doi = doi;
                    release.Source = journal; // Arxiv doesn't provide journal info (preprint server)
                    release.DownloadUrl = pdfLink;
                    release.InfoUrl = id; // Usually the abstract URL
                    release.Container = "PDF";
                    release.Categories = new List<int> { 8000 }; // ebooks/papers
                    release.Size = 0; // Unknown

                    if (DateTime.TryParse(published, out var pubDate))
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
