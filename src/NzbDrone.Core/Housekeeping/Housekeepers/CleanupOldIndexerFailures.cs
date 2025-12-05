using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOldIndexerFailures : IHousekeepingTask
    {
        private readonly IIndexerFailureRepository _failureRepository;

        public CleanupOldIndexerFailures(IIndexerFailureRepository failureRepository)
        {
            _failureRepository = failureRepository;
        }

        public void Clean()
        {
            // Keep failures for the last 30 days
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            _failureRepository.DeleteOldFailures(cutoffDate);
        }
    }
}

