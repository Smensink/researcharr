using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Unpaywall
{
    public class UnpaywallRequestGenerator : IIndexerRequestGenerator
    {
        public UnpaywallSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var url = $"{Settings.BaseUrl}/10.1038/nature12373?email={Settings.Email}";
            chain.Add(new[] { new IndexerRequest(url, HttpAccept.Json) });
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            foreach (var doi in GetDoiQueries(searchCriteria))
            {
                chain.Add(BuildRequests(doi));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        private IEnumerable<IndexerRequest> BuildRequests(string doi)
        {
            var encodedDoi = Uri.EscapeDataString(doi);
            var url = $"{Settings.BaseUrl}/{encodedDoi}?email={Settings.Email}";
            var request = new IndexerRequest(url, HttpAccept.Json);
            yield return request;
        }

        private IEnumerable<string> GetDoiQueries(BookSearchCriteria searchCriteria)
        {
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
                var normalized = NormalizeDoi(candidate);
                if (normalized.IsNotNullOrWhiteSpace())
                {
                    yield return normalized;
                }
            }
        }

        private string NormalizeDoi(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            var trimmed = value.Trim();
            var doiIndex = trimmed.IndexOf("doi.org/", System.StringComparison.OrdinalIgnoreCase);

            if (doiIndex >= 0)
            {
                trimmed = trimmed.Substring(doiIndex + "doi.org/".Length);
            }

            if (trimmed.StartsWith("doi:", System.StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(4);
            }

            if (!trimmed.StartsWith("10.", System.StringComparison.OrdinalIgnoreCase) || !trimmed.Contains("/"))
            {
                return null;
            }

            return trimmed;
        }
    }
}
