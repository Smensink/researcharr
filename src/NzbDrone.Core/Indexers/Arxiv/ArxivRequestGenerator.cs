using System.Collections.Generic;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Arxiv
{
    public class ArxivRequestGenerator : IIndexerRequestGenerator
    {
        public ArxivSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();

            // Use API for latest submissions across all categories as the primary source
            // RSS/Atom feeds are unreliable (often empty)
            chain.Add(BuildRequests("all", 0, 100, "submittedDate", "descending"));
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            if (!string.IsNullOrWhiteSpace(searchCriteria.AuthorQuery))
            {
                chain.Add(BuildRequests(searchCriteria.AuthorQuery, 0, 100));
            }

            if (!string.IsNullOrWhiteSpace(searchCriteria.BookQuery))
            {
                chain.Add(BuildRequests(searchCriteria.BookQuery, 0, 100));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            if (!string.IsNullOrWhiteSpace(searchCriteria.AuthorQuery))
            {
                chain.Add(BuildRequests(searchCriteria.AuthorQuery, 0, 100));
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query, int start, int maxResults, string sortBy = "relevance", string sortOrder = "descending")
        {
            // Arxiv API: http://export.arxiv.org/api/query?search_query=...&start=...&max_results=...
            // Sanitize query to avoid issues with special characters in strict search
            // Using 'all:' allows for broader matching which is safer for titles with special chars
            var sanitizedQuery = System.Text.RegularExpressions.Regex.Replace(query, @"[^a-zA-Z0-9\s]", " ").Trim();
            sanitizedQuery = System.Text.RegularExpressions.Regex.Replace(sanitizedQuery, @"\s+", "+");

            var url = $"{Settings.BaseUrl}/query?search_query=all:{sanitizedQuery}&start={start}&max_results={maxResults}&sortBy={sortBy}&sortOrder={sortOrder}";
            yield return new IndexerRequest(url, HttpAccept.Rss);
        }
    }
}
