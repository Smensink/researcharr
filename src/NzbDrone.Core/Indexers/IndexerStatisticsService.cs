using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace NzbDrone.Core.Indexers
{
    public class IndexerStatisticsService : IIndexerStatisticsService
    {
        private readonly IIndexerFailureRepository _failureRepository;
        private readonly IIndexerSuccessRepository _successRepository;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _statusService;
        private readonly Logger _logger;

        public IndexerStatisticsService(
            IIndexerFailureRepository failureRepository,
            IIndexerSuccessRepository successRepository,
            IIndexerFactory indexerFactory,
            IIndexerStatusService statusService,
            Logger logger)
        {
            _failureRepository = failureRepository;
            _successRepository = successRepository;
            _indexerFactory = indexerFactory;
            _statusService = statusService;
            _logger = logger;
        }

        public List<IndexerStatistics> GetAllStatistics()
        {
            var indexers = _indexerFactory.All();
            var statistics = new List<IndexerStatistics>();

            foreach (var indexer in indexers)
            {
                try
                {
                    var stat = GetStatistics(indexer.Id);
                    if (stat != null)
                    {
                        statistics.Add(stat);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Failed to get statistics for indexer {0}", indexer.Id);
                }
            }

            return statistics;
        }

        public IndexerStatistics GetStatistics(int indexerId)
        {
            var indexer = _indexerFactory.Get(indexerId);
            if (indexer == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var last24Hours = now.AddDays(-1);
            var last7Days = now.AddDays(-7);

            var allFailures = _failureRepository.GetByIndexerId(indexerId);
            var recentFailures = allFailures.Where(f => f.Timestamp >= last24Hours).ToList();
            var weekFailures = allFailures.Where(f => f.Timestamp >= last7Days).ToList();

            var allSuccesses = _successRepository.GetByIndexerId(indexerId);
            var weekSuccesses = allSuccesses.Where(s => s.Timestamp >= last7Days).ToList();

            var status = _statusService.GetStatus(indexerId);

            // Calculate per-operation statistics
            var operationStats = new Dictionary<IndexerOperationType, OperationStatistics>();
            var allOperationTypes = Enum.GetValues(typeof(IndexerOperationType)).Cast<IndexerOperationType>().ToList();

            foreach (var operationType in allOperationTypes)
            {
                var operationFailures = weekFailures.Where(f => f.OperationType == operationType).Count();
                var operationSuccesses = weekSuccesses.Where(s => s.OperationType == operationType).Count();
                var totalOperations = operationFailures + operationSuccesses;

                double failureRate = 0.0;
                if (totalOperations > 0)
                {
                    failureRate = (operationFailures / (double)totalOperations) * 100.0;
                }
                else if (status?.IsDisabled() == true)
                {
                    // If indexer is disabled and no operations, consider it 100% failure
                    failureRate = 100.0;
                }

                operationStats[operationType] = new OperationStatistics
                {
                    Successes = operationSuccesses,
                    Failures = operationFailures,
                    FailureRate = failureRate
                };
            }

            // Calculate overall failure rate (failures / (failures + successes) * 100)
            var totalWeekOperations = weekFailures.Count + weekSuccesses.Count;
            double overallFailureRate = 0.0;
            if (totalWeekOperations > 0)
            {
                overallFailureRate = (weekFailures.Count / (double)totalWeekOperations) * 100.0;
            }
            else if (status?.IsDisabled() == true)
            {
                overallFailureRate = 100.0;
            }

            var lastSuccess = allSuccesses.OrderByDescending(s => s.Timestamp).FirstOrDefault()?.Timestamp;

            var statistics = new IndexerStatistics
            {
                IndexerId = indexerId,
                IndexerName = indexer.Name,
                TotalFailures = allFailures.Count,
                RecentFailures = recentFailures.Count,
                LastFailure = allFailures.OrderByDescending(f => f.Timestamp).FirstOrDefault()?.Timestamp,
                LastSuccess = lastSuccess,
                FailuresByOperation = allFailures
                    .GroupBy(f => f.OperationType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                FailuresByErrorType = allFailures
                    .GroupBy(f => f.ErrorType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                FailureRate = overallFailureRate,
                IsHealthy = recentFailures.Count == 0 && status?.IsDisabled() != true,
                OperationStatistics = operationStats
            };

            return statistics;
        }
    }
}

