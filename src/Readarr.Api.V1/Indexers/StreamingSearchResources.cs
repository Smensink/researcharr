using System.Collections.Generic;

namespace Readarr.Api.V1.Indexers
{
    public class StreamingSearchRequest
    {
        public int? BookId { get; set; }
        public int? AuthorId { get; set; }
    }

    public class SearchSessionResource
    {
        public string SearchId { get; set; }
        public int TotalIndexers { get; set; }
        public int CompletedIndexers { get; set; }
        public bool IsComplete { get; set; }
        public int TotalResults { get; set; }
        public List<IndexerSearchStatusResource> IndexerStatuses { get; set; }

        public SearchSessionResource()
        {
            IndexerStatuses = new List<IndexerSearchStatusResource>();
        }
    }

    public class IndexerSearchStatusResource
    {
        public int IndexerId { get; set; }
        public string IndexerName { get; set; }
        public string State { get; set; }
        public int ResultCount { get; set; }
        public string ErrorMessage { get; set; }
    }
}
