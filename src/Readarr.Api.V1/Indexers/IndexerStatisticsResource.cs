using System;
using System.Collections.Generic;
using NzbDrone.Core.Indexers;

namespace Readarr.Api.V1.Indexers
{
    public class IndexerStatisticsResource
    {
        public int IndexerId { get; set; }
        public string IndexerName { get; set; }
        public int TotalFailures { get; set; }
        public int RecentFailures { get; set; }
        public DateTime? LastFailure { get; set; }
        public DateTime? LastSuccess { get; set; }
        public Dictionary<IndexerOperationType, int> FailuresByOperation { get; set; }
        public Dictionary<IndexerErrorType, int> FailuresByErrorType { get; set; }
        public double FailureRate { get; set; }
        public bool IsHealthy { get; set; }
        public Dictionary<IndexerOperationType, OperationStatisticsResource> OperationStatistics { get; set; }
    }

    public class OperationStatisticsResource
    {
        public int Successes { get; set; }
        public int Failures { get; set; }
        public double FailureRate { get; set; }
        public int TotalOperations { get; set; }
    }
}

