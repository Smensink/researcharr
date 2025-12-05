using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Validation;
using Readarr.Http;

namespace Readarr.Api.V1.Indexers
{
    [V1ApiController]
    public class IndexerController : ProviderControllerBase<IndexerResource, IndexerBulkResource, IIndexer, IndexerDefinition>
    {
        public static readonly IndexerResourceMapper ResourceMapper = new ();
        public static readonly IndexerBulkResourceMapper BulkResourceMapper = new ();
        private readonly IIndexerStatisticsService _statisticsService;
        private readonly IIndexerFailureRepository _failureRepository;

        public IndexerController(IndexerFactory indexerFactory, DownloadClientExistsValidator downloadClientExistsValidator, IIndexerStatisticsService statisticsService, IIndexerFailureRepository failureRepository)
            : base(indexerFactory, "indexer", ResourceMapper, BulkResourceMapper)
        {
            SharedValidator.RuleFor(c => c.Priority).InclusiveBetween(1, 50);
            SharedValidator.RuleFor(c => c.DownloadClientId).SetValidator(downloadClientExistsValidator);
            _statisticsService = statisticsService;
            _failureRepository = failureRepository;
        }

        [HttpGet("statistics")]
        [Produces("application/json")]
        public List<IndexerStatisticsResource> GetAllStatistics()
        {
            var statistics = _statisticsService.GetAllStatistics();
            return statistics.Select(s => new IndexerStatisticsResource
            {
                IndexerId = s.IndexerId,
                IndexerName = s.IndexerName,
                TotalFailures = s.TotalFailures,
                RecentFailures = s.RecentFailures,
                LastFailure = s.LastFailure,
                LastSuccess = s.LastSuccess,
                FailuresByOperation = s.FailuresByOperation,
                FailuresByErrorType = s.FailuresByErrorType,
                FailureRate = s.FailureRate,
                IsHealthy = s.IsHealthy,
                OperationStatistics = s.OperationStatistics?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new OperationStatisticsResource
                    {
                        Successes = kvp.Value.Successes,
                        Failures = kvp.Value.Failures,
                        FailureRate = kvp.Value.FailureRate,
                        TotalOperations = kvp.Value.TotalOperations
                    })
            }).ToList();
        }

        [HttpGet("{id}/statistics")]
        [Produces("application/json")]
        public ActionResult<IndexerStatisticsResource> GetStatistics(int id)
        {
            var statistics = _statisticsService.GetStatistics(id);
            if (statistics == null)
            {
                return NotFound();
            }

            return new IndexerStatisticsResource
            {
                IndexerId = statistics.IndexerId,
                IndexerName = statistics.IndexerName,
                TotalFailures = statistics.TotalFailures,
                RecentFailures = statistics.RecentFailures,
                LastFailure = statistics.LastFailure,
                LastSuccess = statistics.LastSuccess,
                FailuresByOperation = statistics.FailuresByOperation,
                FailuresByErrorType = statistics.FailuresByErrorType,
                FailureRate = statistics.FailureRate,
                IsHealthy = statistics.IsHealthy,
                OperationStatistics = statistics.OperationStatistics?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new OperationStatisticsResource
                    {
                        Successes = kvp.Value.Successes,
                        Failures = kvp.Value.Failures,
                        FailureRate = kvp.Value.FailureRate,
                        TotalOperations = kvp.Value.TotalOperations
                    })
            };
        }

        [HttpGet("{id}/failures")]
        [Produces("application/json")]
        public ActionResult<List<IndexerFailureResource>> GetFailures(
            int id,
            [FromQuery] DateTime? since = null,
            [FromQuery] IndexerOperationType? operationType = null,
            [FromQuery] IndexerErrorType? errorType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            List<IndexerFailure> failures;

            if (since.HasValue)
            {
                failures = _failureRepository.GetByIndexerIdAndDateRange(id, since.Value, DateTime.UtcNow);
            }
            else
            {
                failures = _failureRepository.GetByIndexerId(id);
            }

            // Apply filters
            if (operationType.HasValue)
            {
                failures = failures.Where(f => f.OperationType == operationType.Value).ToList();
            }

            if (errorType.HasValue)
            {
                failures = failures.Where(f => f.ErrorType == errorType.Value).ToList();
            }

            // Apply pagination
            var totalCount = failures.Count;
            var pagedFailures = failures
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var resources = pagedFailures.Select(f => new IndexerFailureResource(f)).ToList();

            // Add pagination headers if needed (could be enhanced with proper pagination response model)
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page", page.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(resources);
        }
    }
}
