using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Indexers.Commands
{
    public class AdjustIndexerPriorityCommandService : IExecute<AdjustIndexerPriorityCommand>
    {
        private readonly IIndexerStatisticsService _statisticsService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public AdjustIndexerPriorityCommandService(
            IIndexerStatisticsService statisticsService,
            IIndexerFactory indexerFactory,
            IConfigService configService,
            Logger logger)
        {
            _statisticsService = statisticsService;
            _indexerFactory = indexerFactory;
            _configService = configService;
            _logger = logger;
        }

        public void Execute(AdjustIndexerPriorityCommand message)
        {
            if (!_configService.AutoAdjustIndexerPriority)
            {
                _logger.Debug("Automatic indexer priority adjustment is disabled");
                return;
            }

            _logger.Info("Starting automatic indexer priority adjustment");

            try
            {
                var statistics = _statisticsService.GetAllStatistics();
                var indexers = _indexerFactory.All();

                // Filter to only enabled indexers
                var enabledIndexers = indexers.Where(i => i.Enable).ToList();

                if (!enabledIndexers.Any())
                {
                    _logger.Debug("No enabled indexers found, skipping priority adjustment");
                    return;
                }

                // Get statistics for enabled indexers only
                var enabledStatistics = statistics
                    .Where(s => enabledIndexers.Any(i => i.Id == s.IndexerId))
                    .ToList();

                // Calculate Bayesian reliability scores for each indexer
                var indexerScores = enabledStatistics.Select(stat =>
                {
                    var indexer = enabledIndexers.FirstOrDefault(i => i.Id == stat.IndexerId);
                    if (indexer == null)
                    {
                        return new { Stat = stat, Score = 0.0, Indexer = (IndexerDefinition)null };
                    }

                    var bayesianScore = CalculateBayesianReliabilityScore(stat);
                    return new { Stat = stat, Score = bayesianScore, Indexer = indexer };
                }).Where(x => x.Indexer != null).ToList();

                // Sort by Bayesian reliability score (descending - higher score = better = lower priority number)
                var sortedScores = indexerScores
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Stat.RecentFailures)
                    .ThenBy(x => x.Stat.TotalFailures)
                    .ToList();

                var changes = new List<string>();
                var priority = 1; // Start with priority 1 (highest priority)

                foreach (var item in sortedScores)
                {
                    var indexer = item.Indexer;
                    var stat = item.Stat;
                    var score = item.Score;

                    // Only update if priority has changed
                    if (indexer.Priority != priority)
                    {
                        var oldPriority = indexer.Priority;
                        indexer.Priority = priority;
                        _indexerFactory.Update(indexer);

                        changes.Add($"{indexer.Name}: {oldPriority} -> {priority} (Bayesian Score: {score:F4}, Failure Rate: {stat.FailureRate:F2}%)");
                    }

                    priority++;

                    // Don't exceed maximum priority (50)
                    if (priority > 50)
                    {
                        break;
                    }
                }

                if (changes.Any())
                {
                    _logger.Info("Adjusted priorities for {0} indexer(s): {1}", changes.Count, string.Join("; ", changes));
                }
                else
                {
                    _logger.Debug("No indexer priorities needed adjustment");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adjusting indexer priorities");
                throw;
            }
        }

        /// <summary>
        /// Calculates a Bayesian reliability score for an indexer.
        /// Uses a prior belief (95% success rate with weight of 100 operations) combined with observed data.
        /// Higher score = more reliable indexer.
        /// </summary>
        private double CalculateBayesianReliabilityScore(IndexerStatistics stat)
        {
            // Prior belief: assume 95% success rate with weight equivalent to 100 operations
            const double priorSuccessRate = 0.95;
            const double priorWeight = 100.0;

            // Estimate total operations from failures
            // We estimate operations based on failure rate calculation assumptions
            // If we have failure data, estimate operations; otherwise use minimal prior
            double estimatedOperations;
            if (stat.TotalFailures > 0)
            {
                // Estimate operations: assume failure rate calculation used ~100 ops/day for 7 days
                // But scale based on actual failure count to get better estimate
                // If failure rate is X%, then: failures / total_ops = X/100
                // So: total_ops = failures / (X/100) = failures * 100 / X
                if (stat.FailureRate > 0)
                {
                    estimatedOperations = stat.TotalFailures * 100.0 / stat.FailureRate;
                }
                else
                {
                    // If failure rate is 0 but we have failures (shouldn't happen), use minimum
                    estimatedOperations = Math.Max(stat.TotalFailures * 10.0, priorWeight);
                }
            }
            else
            {
                // No failures observed - use a conservative estimate based on time indexer has been active
                // Assume at least 50 operations for an active indexer
                estimatedOperations = Math.Max(50.0, priorWeight);
            }

            // Calculate observed success rate
            var observedSuccesses = Math.Max(0, estimatedOperations - stat.TotalFailures);
            var observedSuccessRate = estimatedOperations > 0 ? observedSuccesses / estimatedOperations : 1.0;
            var observedWeight = estimatedOperations;

            // Bayesian average: combine prior and observed data
            // Score = (prior_success_rate * prior_weight + observed_success_rate * observed_weight) / (prior_weight + observed_weight)
            var bayesianScore = (priorSuccessRate * priorWeight + observedSuccessRate * observedWeight) / (priorWeight + observedWeight);

            // Apply penalty for recent failures (last 24 hours) to make the score more responsive
            // Each recent failure reduces the score by a small amount
            var recentFailurePenalty = stat.RecentFailures * 0.01; // 1% penalty per recent failure
            bayesianScore = Math.Max(0.0, bayesianScore - recentFailurePenalty);

            // Apply penalty if indexer is unhealthy
            if (!stat.IsHealthy)
            {
                bayesianScore *= 0.8; // 20% penalty for unhealthy indexers
            }

            return bayesianScore;
        }
    }
}

