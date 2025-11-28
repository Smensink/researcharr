using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Doaj
{
    public class DoajRequestGenerator : IIndexerRequestGenerator
    {
        public DoajSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var url = $"{Settings.BaseUrl}/search/articles/the?page=1&pageSize=1";
            chain.Add(new[] { new IndexerRequest(url, HttpAccept.Json) });
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"bibjson.author.name:\"{searchCriteria.AuthorQuery}\""));
            }

            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"bibjson.title:\"{searchCriteria.BookQuery}\""));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"bibjson.author.name:\"{searchCriteria.AuthorQuery}\""));
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query)
        {
            var url = $"{Settings.BaseUrl}/search/articles/{query}?page=1&pageSize=100";
            var request = new IndexerRequest(url, HttpAccept.Json);
            yield return request;
        }
    }
}
