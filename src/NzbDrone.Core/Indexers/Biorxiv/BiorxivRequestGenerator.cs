using System;
using System.Collections.Generic;
using System.Web;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Biorxiv
{
    public class BiorxivRequestGenerator : IIndexerRequestGenerator
    {
        private const int PageSize = 100;
        private const int RecentWindowDays = 7;
        private const int SearchWindowYears = 3;
        private const int MaxPages = 2;

        public IBiorxivSettings Settings { get; set; }
        public string Server { get; set; }

        public IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var toDate = DateTime.UtcNow.Date;
            var fromDate = toDate.AddDays(-RecentWindowDays);

            chain.Add(BuildDateRangeRequests(fromDate, toDate, 1, null));
            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            foreach (var doi in GetDoiQueries(searchCriteria))
            {
                chain.Add(BuildDoiRequest(doi));
            }

            var titleQuery = SafeQuery(searchCriteria.BookTitle);
            var authorQuery = SafeQuery(searchCriteria.Author?.Name);
            var query = BuildSearchQuery(titleQuery, authorQuery);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var toDate = DateTime.UtcNow.Date;
                var fromDate = toDate.AddYears(-SearchWindowYears);
                chain.Add(BuildDateRangeRequests(fromDate, toDate, MaxPages, query));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            var authorQuery = SafeQuery(searchCriteria.Author?.Name);
            if (!string.IsNullOrWhiteSpace(authorQuery))
            {
                var toDate = DateTime.UtcNow.Date;
                var fromDate = toDate.AddYears(-SearchWindowYears);
                chain.Add(BuildDateRangeRequests(fromDate, toDate, MaxPages, authorQuery));
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildDateRangeRequests(DateTime fromDate, DateTime toDate, int maxPages, string query)
        {
            var baseUrl = Settings.BaseUrl.TrimEnd('/');
            var queryPart = string.IsNullOrWhiteSpace(query) ? string.Empty : $"?q={HttpUtility.UrlEncode(query)}";

            for (var page = 0; page < maxPages; page++)
            {
                var cursor = page * PageSize;
                var url = $"{baseUrl}/{Server}/{fromDate:yyyy-MM-dd}/{toDate:yyyy-MM-dd}/{cursor}{queryPart}";

                yield return new IndexerRequest(url, HttpAccept.Json);
            }
        }

        private IEnumerable<IndexerRequest> BuildDoiRequest(string doi)
        {
            var baseUrl = Settings.BaseUrl.TrimEnd('/');
            var url = $"{baseUrl}/{Server}/{doi.Trim()}";

            yield return new IndexerRequest(url, HttpAccept.Json);
        }

        private IEnumerable<string> GetDoiQueries(BookSearchCriteria searchCriteria)
        {
            // Prioritize BookDoi field (most reliable)
            if (!string.IsNullOrWhiteSpace(searchCriteria.BookDoi))
            {
                yield return searchCriteria.BookDoi;
                yield break; // If we have explicit DOI, don't try to extract from other fields
            }

            // Fallback: try to extract DOI from other fields
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(searchCriteria.BookTitle))
            {
                candidates.Add(searchCriteria.BookTitle);
            }

            if (!string.IsNullOrWhiteSpace(searchCriteria.BookIsbn))
            {
                candidates.Add(searchCriteria.BookIsbn);
            }

            if (!string.IsNullOrWhiteSpace(searchCriteria.Disambiguation))
            {
                candidates.Add(searchCriteria.Disambiguation);
            }

            foreach (var candidate in candidates)
            {
                var normalized = DoiUtility.Normalize(candidate);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    yield return normalized;
                }
            }
        }

        private string BuildSearchQuery(string title, string author)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(title))
            {
                parts.Add(title);
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                parts.Add(author);
            }

            return string.Join(" ", parts);
        }

        private string SafeQuery(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Replace("+", " ").Trim();
        }
    }
}
