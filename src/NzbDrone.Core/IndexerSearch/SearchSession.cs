using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.IndexerSearch
{
    public class SearchSession
    {
        public string SearchId { get; set; }
        public SearchCriteriaBase Criteria { get; set; }
        public ConcurrentDictionary<string, DownloadDecision> BestDecisionByGuid { get; set; }
        public HashSet<int> CompletedIndexers { get; set; }
        public HashSet<int> PendingIndexers { get; set; }
        public Dictionary<int, IndexerSearchStatus> IndexerStatuses { get; set; }
        public bool IsComplete { get; set; }
        public DateTime StartedAt { get; set; }
        public int? BookId { get; set; }
        public int? AuthorId { get; set; }

        public SearchSession()
        {
            SearchId = Guid.NewGuid().ToString("N");
            BestDecisionByGuid = new ConcurrentDictionary<string, DownloadDecision>();
            CompletedIndexers = new HashSet<int>();
            PendingIndexers = new HashSet<int>();
            IndexerStatuses = new Dictionary<int, IndexerSearchStatus>();
            StartedAt = DateTime.UtcNow;
        }

        public int TotalIndexers => PendingIndexers.Count + CompletedIndexers.Count;
        public int CompletedCount => CompletedIndexers.Count;
    }

    public class IndexerSearchStatus
    {
        public int IndexerId { get; set; }
        public string IndexerName { get; set; }
        public SearchIndexerState State { get; set; }
        public int ResultCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum SearchIndexerState
    {
        Pending,
        Searching,
        Completed,
        Failed
    }
}
