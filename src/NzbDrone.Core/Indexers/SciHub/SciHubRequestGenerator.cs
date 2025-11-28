using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.SciHub
{
    public class SciHubRequestGenerator : IIndexerRequestGenerator
    {
        public SciHubSettings Settings { get; set; }
        public IHttpClient HttpClient { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var mirrors = Settings?.Mirrors?.Split(new[] { '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            var firstMirror = mirrors?
                .Select(v => v.Trim())
                .FirstOrDefault(v => v.IsNotNullOrWhiteSpace());
            if (firstMirror.IsNotNullOrWhiteSpace())
            {
                chain.Add(new[] { new IndexerRequest(HttpUri.CombinePath(firstMirror, string.Empty), HttpAccept.Html) });
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            foreach (var query in BuildQueries(searchCriteria))
            {
                chain.Add(BuildRequests(query));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        private IEnumerable<string> BuildQueries(BookSearchCriteria searchCriteria)
        {
            if (searchCriteria.BookIsbn.IsNotNullOrWhiteSpace())
            {
                var normalized = NormalizeQuery(searchCriteria.BookIsbn);
                if (normalized.IsNotNullOrWhiteSpace())
                {
                    yield return normalized;
                }
            }

            if (searchCriteria.Books != null)
            {
                foreach (var book in searchCriteria.Books)
                {
                    if (book.Editions?.Value == null)
                    {
                        continue;
                    }

                    foreach (var edition in book.Editions.Value.Where(e => e.Isbn13.IsNotNullOrWhiteSpace()))
                    {
                        var normalized = NormalizeQuery(edition.Isbn13);
                        if (normalized.IsNotNullOrWhiteSpace())
                        {
                            yield return normalized;
                        }
                    }
                }
            }

            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace())
            {
                var normalized = NormalizeQuery(searchCriteria.BookQuery);
                if (normalized.IsNotNullOrWhiteSpace())
                {
                    yield return normalized;
                }
            }
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query)
        {
            if (query.IsNullOrWhiteSpace())
            {
                yield break;
            }

            var mirrors = Settings.Mirrors.Split(new[] { '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            var variants = BuildQueryVariants(query);

            foreach (var mirror in mirrors)
            {
                var baseUrl = mirror.Trim();
                if (baseUrl.IsNullOrWhiteSpace())
                {
                    continue;
                }

                foreach (var q in variants)
                {
                    var url = $"{baseUrl}/https://doi.org/{q}";
                    if (Settings.FlareSolverrUrl.IsNotNullOrWhiteSpace() && HttpClient != null)
                    {
                        var solverUrl = Settings.FlareSolverrUrl.TrimEnd('/');
                        var payload = new
                        {
                            cmd = "request.get",
                            url,
                            maxTimeout = 60000
                        };

                        var httpRequest = new HttpRequest(solverUrl + "/v1", HttpAccept.Json)
                        {
                            Method = HttpMethod.Post
                        };
                        httpRequest.SetContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload));
                        httpRequest.Headers.ContentType = "application/json";

                        yield return new IndexerRequest(httpRequest);
                    }
                    else
                    {
                        var request = new IndexerRequest(url, HttpAccept.Html);
                        yield return request;
                    }
                }
            }
        }

        private string NormalizeQuery(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            var trimmed = value.Trim();

            if (trimmed.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Contains("doi.org/", System.StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed[(trimmed.IndexOf("doi.org/", System.StringComparison.OrdinalIgnoreCase) + "doi.org/".Length) ..];
                }
            }

            if (trimmed.StartsWith("doi:", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(4);
            }

            // If it looks like a DOI, keep as-is; otherwise return original trimmed value
            if (trimmed.StartsWith("10.", System.StringComparison.OrdinalIgnoreCase) && trimmed.Contains("/"))
            {
                return trimmed;
            }

            return value.Trim();
        }

        private IEnumerable<string> BuildQueryVariants(string query)
        {
            var normalized = NormalizeQuery(query);

            if (normalized.IsNotNullOrWhiteSpace() && normalized.StartsWith("10.", StringComparison.OrdinalIgnoreCase))
            {
                // Only hit Sci-Hub with DOI URL and bare DOI forms to avoid wasting requests.
                return new[] { normalized };
            }

            return Array.Empty<string>();
        }
    }
}
