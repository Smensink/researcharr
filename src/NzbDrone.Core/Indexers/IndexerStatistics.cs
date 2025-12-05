using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Indexers
{
    public class IndexerStatistics
    {
        public int IndexerId { get; set; }
        public string IndexerName { get; set; }
        public int TotalFailures { get; set; }
        public int RecentFailures { get; set; } // Last 24 hours
        public DateTime? LastFailure { get; set; }
        public DateTime? LastSuccess { get; set; }
        public Dictionary<IndexerOperationType, int> FailuresByOperation { get; set; }
        public Dictionary<IndexerErrorType, int> FailuresByErrorType { get; set; }
        public double FailureRate { get; set; } // Overall failure rate percentage (failures / (failures + successes) * 100)
        public bool IsHealthy { get; set; }
        
        // Per-operation statistics
        public Dictionary<IndexerOperationType, OperationStatistics> OperationStatistics { get; set; }
    }

    public class OperationStatistics
    {
        public int Successes { get; set; }
        public int Failures { get; set; }
        public double FailureRate { get; set; } // failures / (failures + successes) * 100
        public int TotalOperations => Successes + Failures;
    }
}

