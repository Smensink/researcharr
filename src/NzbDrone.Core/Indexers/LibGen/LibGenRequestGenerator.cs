using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.LibGen
{
    public class LibGenRequestGenerator : IIndexerRequestGenerator
    {
        public LibGenSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var mirrors = Settings?.Mirrors?.Split(new[] { '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            var firstMirror = mirrors?.FirstOrDefault(v => v.IsNotNullOrWhiteSpace())?.Trim().TrimEnd('/');

            if (firstMirror.IsNotNullOrWhiteSpace())
            {
                var baseUrl = NormalizeBase(firstMirror);

                // Try multiple RSS/recent endpoints - different mirrors use different patterns
                var recentRequests = new List<IndexerRequest>
                {
                    BuildRequest($"{baseUrl}/rss/index.php", HttpAccept.Rss),
                    BuildRequest($"{baseUrl}/rss.php", HttpAccept.Rss),
                    BuildRequest($"{baseUrl}/index.php?res=25&view=simple", HttpAccept.Html),
                    BuildRequest($"{baseUrl}/search.php?req=&res=25", HttpAccept.Html),
                    BuildRequest($"{baseUrl}/json.php?fields=Title,Author&ids=1,2,3", HttpAccept.Json),
                };

                chain.Add(recentRequests);
            }

            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            // Prioritize DOI search if available (most reliable identifier)
            if (searchCriteria.BookDoi.IsNotNullOrWhiteSpace())
            {
                var normalizedDoi = Parser.DoiUtility.Normalize(searchCriteria.BookDoi);
                if (normalizedDoi.IsNotNullOrWhiteSpace())
                {
                    // LibGen may not support direct DOI search, but try it anyway
                    chain.Add(BuildRequests(normalizedDoi, "title"));
                }
            }

            // Fallback to title/author search
            BuildTitleAndAuthorRequests(chain, searchCriteria.BookQuery, searchCriteria.AuthorQuery);

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            BuildTitleAndAuthorRequests(chain, null, searchCriteria.AuthorQuery);

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query, string column)
        {
            var mirrors = Settings.Mirrors.Split(new[] { '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            var encodedQuery = Uri.EscapeDataString(query);

            foreach (var mirror in mirrors)
            {
                var baseUrl = NormalizeBase(mirror);
                if (baseUrl.IsNullOrWhiteSpace())
                {
                    continue;
                }

                // libgen.li uses index.php for search (search.php returns 404)
                // Pattern 1: index.php with search params (libgen.li - WORKING)
                yield return BuildRequest($"{baseUrl}/index.php?req={encodedQuery}&res=100");

                // Pattern 2: Root with query params (libgen.li alternative - WORKING)
                yield return BuildRequest($"{baseUrl}/?req={encodedQuery}&res=100");

                // Pattern 3: search.php for libgen.is/rs mirrors
                yield return BuildRequest($"{baseUrl}/search.php?req={encodedQuery}&column={column}&res=100");

                // Pattern 4: Fiction section (for books specifically)
                if (column == "title" || column == "author")
                {
                    yield return BuildRequest($"{baseUrl}/fiction/?q={encodedQuery}&criteria={column}&format=");
                }
            }
        }

        private void BuildTitleAndAuthorRequests(IndexerPageableRequestChain chain, string titleQuery, string authorQuery)
        {
            if (titleQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests(titleQuery, "title"));
            }

            if (authorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests(authorQuery, "author"));
            }

            // If both are present, also try combined query to improve match quality.
            if (titleQuery.IsNotNullOrWhiteSpace() && authorQuery.IsNotNullOrWhiteSpace())
            {
                var combined = $"{titleQuery} {authorQuery}";
                chain.Add(BuildRequests(combined, "title"));
            }
        }

        private IndexerRequest BuildRequest(string url, HttpAccept acceptOverride = null)
        {
            if (Settings.FlareSolverrUrl.IsNotNullOrWhiteSpace())
            {
                var solverUrl = Settings.FlareSolverrUrl.TrimEnd('/');
                var payload = new
                {
                    cmd = "request.get",
                    url,
                    maxTimeout = 90000
                };

                var httpRequest = new HttpRequest($"{solverUrl}/v1", HttpAccept.Json)
                {
                    Method = HttpMethod.Post
                };
                httpRequest.SetContent(JsonConvert.SerializeObject(payload));
                httpRequest.Headers.ContentType = "application/json";
                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;
                httpRequest.SuppressHttpErrorStatusCodes = new[]
                {
                    HttpStatusCode.MovedPermanently, HttpStatusCode.Redirect, HttpStatusCode.RedirectKeepVerb,
                    HttpStatusCode.RedirectMethod, HttpStatusCode.Found
                };

                return new IndexerRequest(httpRequest);
            }

            var request = new IndexerRequest(url, acceptOverride ?? HttpAccept.Html);
            request.HttpRequest.RequestTimeout = TimeSpan.FromSeconds(30);
            request.HttpRequest.AllowAutoRedirect = true;
            request.HttpRequest.SuppressHttpError = true;
            request.HttpRequest.SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.MovedPermanently, HttpStatusCode.Redirect, HttpStatusCode.RedirectKeepVerb, HttpStatusCode.RedirectMethod, HttpStatusCode.Found };

            // Add browser-like headers to bypass basic anti-bot detection
            request.HttpRequest.Headers.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.HttpRequest.Headers.Set("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            request.HttpRequest.Headers.Set("Accept-Language", "en-US,en;q=0.9");
            request.HttpRequest.Headers.Set("Accept-Encoding", "gzip, deflate");
            request.HttpRequest.Headers.Set("DNT", "1");
            request.HttpRequest.Headers.Set("Connection", "keep-alive");
            request.HttpRequest.Headers.Set("Upgrade-Insecure-Requests", "1");
            request.HttpRequest.Headers.Set("Sec-Fetch-Dest", "document");
            request.HttpRequest.Headers.Set("Sec-Fetch-Mode", "navigate");
            request.HttpRequest.Headers.Set("Sec-Fetch-Site", "none");
            request.HttpRequest.Headers.Set("Sec-Fetch-User", "?1");
            request.HttpRequest.Headers.Set("Cache-Control", "max-age=0");

            return request;
        }

        private string NormalizeBase(string mirror)
        {
            if (mirror.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            var trimmed = mirror.Trim().TrimEnd('/');

            if (trimmed.EndsWith("index.php", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^"index.php".Length].TrimEnd('/');
            }
            else if (trimmed.EndsWith("search.php", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^"search.php".Length].TrimEnd('/');
            }

            return trimmed;
        }
    }
}
