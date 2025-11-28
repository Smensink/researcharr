using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Core
{
    public class CoreRequestGenerator : IIndexerRequestGenerator
    {
        public CoreSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var chain = new IndexerPageableRequestChain();
            var url = $"{Settings.BaseUrl}/search/works?q=the&limit=1";
            chain.Add(new[] { new IndexerRequest(url, HttpAccept.Json) });
            return chain;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"authors:\"{searchCriteria.AuthorQuery}\""));
            }

            if (searchCriteria.BookQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"title:\"{searchCriteria.BookQuery}\""));
            }

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AuthorSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            if (searchCriteria.AuthorQuery.IsNotNullOrWhiteSpace())
            {
                chain.Add(BuildRequests($"authors:\"{searchCriteria.AuthorQuery}\""));
            }

            return chain;
        }

        private IEnumerable<IndexerRequest> BuildRequests(string query)
        {
            var url = $"{Settings.BaseUrl}/search/works?q={query}&limit=100";
            var request = new IndexerRequest(url, HttpAccept.Json);

            if (Settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                request.HttpRequest.Headers.Add("Authorization", $"Bearer {Settings.ApiKey}");
            }

            yield return request;
        }
    }
}
