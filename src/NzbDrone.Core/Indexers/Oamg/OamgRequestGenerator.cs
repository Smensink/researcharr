using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Oamg
{
    public class OamgRequestGenerator : IIndexerRequestGenerator
    {
        public OamgSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var url = $"{Settings.BaseUrl}/works?search=the&per-page=1&sort=publication_date:desc";
            chain.Add(new[] { new IndexerRequest(url, HttpAccept.Json) });
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests(searchCriteria.AuthorQuery));
            }

            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests(searchCriteria.BookQuery));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests(searchCriteria.AuthorQuery));
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query)
        {
            var trimmed = query?.Trim();
            if (trimmed.IsNullOrWhiteSpace())
            {
                yield break;
            }

            var topicSearch = trimmed.StartsWith("topic:", StringComparison.OrdinalIgnoreCase) ||
                              trimmed.StartsWith("concept:", StringComparison.OrdinalIgnoreCase);
            var topicTerm = string.Empty;
            if (topicSearch)
            {
                var colonIndex = trimmed.IndexOf(':');
                topicTerm = colonIndex >= 0 && colonIndex < trimmed.Length - 1
                    ? trimmed[(colonIndex + 1) ..].Trim()
                    : string.Empty;
            }

            var encoded = Uri.EscapeDataString(trimmed);
            var baseUrl = Settings.BaseUrl.TrimEnd('/');
            var url = topicSearch
                ? $"{baseUrl}/works?filter=concepts.display_name.search:{Uri.EscapeDataString(topicTerm)}&per-page=100&sort=cited_by_count:desc"
                : $"{baseUrl}/works?search={encoded}&per-page=100&sort=cited_by_count:desc";
            var request = new IndexerRequest(url, HttpAccept.Json);
            yield return request;
        }
    }
}
