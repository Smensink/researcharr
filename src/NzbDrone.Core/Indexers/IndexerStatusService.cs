using System;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.ThingiProvider.Status;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerStatusService : IProviderStatusServiceBase<IndexerStatus>
    {
        ReleaseInfo GetLastRssSyncReleaseInfo(int indexerId);

        void UpdateRssSyncStatus(int indexerId, ReleaseInfo releaseInfo);

        void RecordFailure(int indexerId, IndexerOperationType operation, IndexerErrorType errorType, string errorMessage, int? httpStatusCode = null);

        void RecordSuccess(int indexerId, IndexerOperationType operation);

        IndexerStatus GetStatus(int indexerId);
    }

    public class IndexerStatusService : ProviderStatusServiceBase<IIndexer, IndexerStatus>, IIndexerStatusService
    {
        private readonly IIndexerFailureRepository _failureRepository;
        private readonly IIndexerSuccessRepository _successRepository;

        public IndexerStatusService(IIndexerStatusRepository providerStatusRepository, IIndexerFailureRepository failureRepository, IIndexerSuccessRepository successRepository, IEventAggregator eventAggregator, IRuntimeInfo runtimeInfo, Logger logger)
            : base(providerStatusRepository, eventAggregator, runtimeInfo, logger)
        {
            _failureRepository = failureRepository;
            _successRepository = successRepository;
        }

        public ReleaseInfo GetLastRssSyncReleaseInfo(int indexerId)
        {
            return GetProviderStatus(indexerId).LastRssSyncReleaseInfo;
        }

        public void UpdateRssSyncStatus(int indexerId, ReleaseInfo releaseInfo)
        {
            lock (_syncRoot)
            {
                var status = GetProviderStatus(indexerId);

                status.LastRssSyncReleaseInfo = releaseInfo;

                _providerStatusRepository.Upsert(status);
            }
        }

        public void RecordFailure(int indexerId, IndexerOperationType operation, IndexerErrorType errorType, string errorMessage, int? httpStatusCode = null)
        {
            try
            {
                var failure = new IndexerFailure
                {
                    IndexerId = indexerId,
                    OperationType = operation,
                    ErrorType = errorType,
                    ErrorMessage = errorMessage?.Substring(0, Math.Min(errorMessage?.Length ?? 0, 1000)), // Limit message length
                    HttpStatusCode = httpStatusCode,
                    Timestamp = DateTime.UtcNow
                };

                _failureRepository.Insert(failure);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to record indexer failure for indexer {0}", indexerId);
            }
        }

        public void RecordSuccess(int indexerId, IndexerOperationType operation)
        {
            try
            {
                var success = new IndexerSuccess
                {
                    IndexerId = indexerId,
                    OperationType = operation,
                    Timestamp = DateTime.UtcNow
                };

                _successRepository.Insert(success);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to record indexer success for indexer {0}", indexerId);
            }
        }

        public IndexerStatus GetStatus(int indexerId)
        {
            return GetProviderStatus(indexerId);
        }
    }
}
