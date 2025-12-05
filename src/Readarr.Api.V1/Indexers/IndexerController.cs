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

        public IndexerController(IndexerFactory indexerFactory, DownloadClientExistsValidator downloadClientExistsValidator, IIndexerStatisticsService statisticsService)
            : base(indexerFactory, "indexer", ResourceMapper, BulkResourceMapper)
        {
            SharedValidator.RuleFor(c => c.Priority).InclusiveBetween(1, 50);
            SharedValidator.RuleFor(c => c.DownloadClientId).SetValidator(downloadClientExistsValidator);
            _statisticsService = statisticsService;
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
                IsHealthy = s.IsHealthy
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
                IsHealthy = statistics.IsHealthy
            };
        }
    }
}
