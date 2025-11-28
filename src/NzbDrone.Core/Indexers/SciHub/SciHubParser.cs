using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.SciHub
{
    public class SciHubParser : IParseIndexerResponse
    {
        public SciHubSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // SciHub often returns 404 if not found; 403/redirects happen for blocked mirrors. Skip these without failing the whole search.
                if (indexerResponse.HttpResponse.StatusCode is System.Net.HttpStatusCode.NotFound
                    or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.Moved
                    or System.Net.HttpStatusCode.MovedPermanently
                    or System.Net.HttpStatusCode.Redirect
                    or System.Net.HttpStatusCode.RedirectKeepVerb
                    or System.Net.HttpStatusCode.RedirectMethod)
                {
                    return releases;
                }

                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            var (html, baseUrl) = ExtractContent(indexerResponse);

            var pdfUrl = FindPdfUrl(html);

            if (pdfUrl.IsNotNullOrWhiteSpace())
            {
                if (pdfUrl.StartsWith("//"))
                {
                    pdfUrl = "https:" + pdfUrl;
                }
                else if (pdfUrl.StartsWith("/"))
                {
                    // Resolve relative URL against the request URL (which is the mirror used)
                    var baseUri = new Uri(baseUrl);
                    pdfUrl = $"{baseUri.Scheme}://{baseUri.Host}{pdfUrl}";
                }

                // Try to extract title/author/doi from meta tags first
                var metaTitle = Regex.Match(html, @"<meta\s+name=[""']citation_title[""']\s+content=[""']([^""]+)[""']", RegexOptions.IgnoreCase);
                var metaAuthor = Regex.Match(html, @"<meta\s+name=[""']citation_author[""']\s+content=[""']([^""]+)[""']", RegexOptions.IgnoreCase);
                var metaDoi = Regex.Match(html, @"<meta\s+name=[""']citation_doi[""']\s+content=[""']([^""]+)[""']", RegexOptions.IgnoreCase);

                var titleMatch = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                
                var doi = DoiUtility.ExtractFromText(baseUrl) ?? 
                          DoiUtility.ExtractFromText(pdfUrl) ?? 
                          (metaDoi.Success ? metaDoi.Groups[1].Value : null) ??
                          DoiUtility.ExtractFromText(html);

                var fallbackTitle = doi.IsNotNullOrWhiteSpace() ? doi : "Unknown SciHub Paper";
                var title = metaTitle.Success ? metaTitle.Groups[1].Value : titleMatch.Success ? titleMatch.Groups[1].Value : fallbackTitle;

                // Clean up title (SciHub often puts "Sci-Hub | " or "Sci-Hub : " prefix)
                title = Regex.Replace(title, @"^Sci-Hub\s*[\|\:]\s*", "", RegexOptions.IgnoreCase);

                if (title.Contains("{title}", StringComparison.OrdinalIgnoreCase) || title.Contains("{doi}", StringComparison.OrdinalIgnoreCase))
                {
                    title = fallbackTitle;
                }

                var author = metaAuthor.Success ? metaAuthor.Groups[1].Value : null;

                var release = new ReleaseInfo();
                release.Guid = $"SciHub-{Guid.NewGuid()}"; // Random GUID since we don't have a stable ID from search
                release.Title = $"{author} - {title}";
                release.Book = title;
                release.Author = author;
                release.Doi = doi;
                release.Container = "PDF";
                release.DownloadUrl = pdfUrl;
                release.InfoUrl = baseUrl;
                release.Size = 0; // Unknown size until downloaded
                release.PublishDate = DateTime.UtcNow;
                release.DownloadProtocol = DownloadProtocol.Http;
                release.Doi = doi;

                releases.Add(release);
            }

            return releases;
        }

        private static string FindPdfUrl(string html)
        {
            // Accept a broad set of Sci-Hub patterns to avoid fragility across mirrors/templates.
            var patterns = new[]
            {
                @"src=['""]([^'""]+)['""][^>]*id=['""]pdf['""]",
                @"<iframe[^>]+src=['""]([^'""]+\.pdf[^'""]*)['""]",
                @"<embed[^>]+src=['""]([^'""]+\.pdf[^'""]*)['""]",
                @"data-src=['""]([^'""]+\.pdf[^'""]*)['""]",
                @"href=['""]([^'""]+\.pdf[^'""]*)['""]",
                @"location\.href\s*=\s*['""]([^'""]+\.pdf[^'""]*)['""]",
                @"http-equiv=['""]refresh['""][^>]+url=([^\""' >]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private static (string Html, string BaseUrl) ExtractContent(IndexerResponse indexerResponse)
        {
            var content = indexerResponse.Content;
            var baseUrl = indexerResponse.HttpRequest.Url.FullUri;

            // Unwrap FlareSolverr JSON responses if present
            if (content.TrimStart().StartsWith("{"))
            {
                try
                {
                    var json = JObject.Parse(content);
                    var solution = json["solution"];
                    var responseHtml = solution?["response"]?.ToString();
                    var solvedUrl = solution?["url"]?.ToString();

                    if (!responseHtml.IsNullOrWhiteSpace())
                    {
                        if (!solvedUrl.IsNullOrWhiteSpace())
                        {
                            baseUrl = solvedUrl;
                        }

                        return (responseHtml, baseUrl);
                    }
                }
                catch
                {
                    // fall back to raw content
                }
            }

            return (content, baseUrl);
        }


    }
}
