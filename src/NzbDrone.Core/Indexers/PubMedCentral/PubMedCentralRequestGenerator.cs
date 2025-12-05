using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.PubMedCentral
{
    public class PubMedCentralRequestGenerator : IIndexerRequestGenerator
    {
        public PubMedCentralSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            // PMC doesn't have a great "recent" endpoint, so we'll search for recent papers
            var chain = new IndexerPageableRequestChain();
            var url = BuildSearchUrl("((free fulltext[filter]) AND (hasabstract[text]))");
            chain.Add(new[] { new IndexerRequest(url, HttpAccept.Rss) });
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            // Try DOI-based search first if available
            var doiQueries = GetDoiQueries(searchCriteria);
            foreach (var doi in doiQueries)
            {
                chain.Add(BuildDoiRequests(doi));
            }

            // Title/Author search
            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace() || searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                var query = BuildSearchQuery(searchCriteria.BookQuery, searchCriteria.AuthorQuery);
                var url = BuildSearchUrl(query);
                chain.Add(new[] { new IndexerRequest(url, HttpAccept.Rss) });
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                var query = BuildSearchQuery(null, searchCriteria.AuthorQuery);
                var url = BuildSearchUrl(query);
                chain.Add(new[] { new IndexerRequest(url, HttpAccept.Rss) });
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildDoiRequests(string doi)
        {
            // Search by DOI in PMC
            var query = $"\"{doi}\"[DOI]";
            var url = BuildSearchUrl(query);
            yield return new IndexerRequest(url, HttpAccept.Rss);
        }

        private string BuildSearchUrl(string query)
        {
            var baseUrl = Settings.BaseUrl.TrimEnd('/');

            // PMC E-utilities search: esearch to get IDs, then efetch to get details
            // We'll use esearch with retmode=xml to get PMC IDs
            var urlBuilder = $"{baseUrl}/esearch.fcgi?db=pmc&term={HttpUtility.UrlEncode(query)}&retmax=100&retmode=xml&usehistory=y";

            if (Settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                urlBuilder += $"&api_key={Settings.ApiKey}";
            }

            if (Settings.Email.IsNotNullOrWhiteSpace())
            {
                urlBuilder += $"&email={HttpUtility.UrlEncode(Settings.Email)}";
            }

            return urlBuilder;
        }

        private string BuildSearchQuery(string title, string author)
        {
            var parts = new List<string>();

            if (title.IsNotNullOrWhiteSpace())
            {
                parts.Add($"\"{title}\"[Title]");
            }

            if (author.IsNotNullOrWhiteSpace())
            {
                parts.Add($"\"{author}\"[Author]");
            }

            // Only return papers with free full text
            parts.Add("(free fulltext[filter])");

            return string.Join(" AND ", parts);
        }

        private IEnumerable<string> GetDoiQueries(BookSearchCriteria searchCriteria)
        {
            // Prioritize BookDoi field (most reliable)
            if (searchCriteria.BookDoi.IsNotNullOrWhiteSpace())
            {
                yield return searchCriteria.BookDoi;
                yield break; // If we have explicit DOI, don't try to extract from other fields
            }

            // Fallback: try to extract DOI from other fields
            var candidates = new List<string>();

            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace())
            {
                candidates.Add(searchCriteria.BookQuery);
            }

            if (searchCriteria.BookIsbn.IsNotNullOrWhiteSpace())
            {
                candidates.Add(searchCriteria.BookIsbn);
            }

            if (searchCriteria.Disambiguation.IsNotNullOrWhiteSpace())
            {
                candidates.Add(searchCriteria.Disambiguation);
            }

            foreach (var candidate in candidates)
            {
                var normalized = Parser.DoiUtility.Normalize(candidate);
                if (normalized.IsNotNullOrWhiteSpace())
                {
                    yield return normalized;
                }
            }
        }
    }
}
