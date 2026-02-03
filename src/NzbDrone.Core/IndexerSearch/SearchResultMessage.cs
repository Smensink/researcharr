using System.Collections.Generic;

namespace NzbDrone.Core.IndexerSearch
{
    public class SearchResultMessage
    {
        public string SearchId { get; set; }
        public string Action { get; set; }
        public int? IndexerId { get; set; }
        public string IndexerName { get; set; }
        public List<object> Results { get; set; }
        public int TotalIndexers { get; set; }
        public int CompletedIndexers { get; set; }
        public Dictionary<int, IndexerSearchStatusDto> IndexerStatuses { get; set; }

        public SearchResultMessage()
        {
            Results = new List<object>();
            IndexerStatuses = new Dictionary<int, IndexerSearchStatusDto>();
        }

        public static class Actions
        {
            public const string Results = "results";
            public const string IndexerComplete = "indexerComplete";
            public const string IndexerFailed = "indexerFailed";
            public const string Complete = "complete";
        }
    }

    public class IndexerSearchStatusDto
    {
        public int IndexerId { get; set; }
        public string IndexerName { get; set; }
        public string State { get; set; }
        public int ResultCount { get; set; }
        public string ErrorMessage { get; set; }
    }
}
