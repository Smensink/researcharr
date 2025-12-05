using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOldIndexerSuccesses : IHousekeepingTask
    {
        private readonly IIndexerSuccessRepository _indexerSuccessRepository;

        public CleanupOldIndexerSuccesses(IIndexerSuccessRepository indexerSuccessRepository)
        {
            _indexerSuccessRepository = indexerSuccessRepository;
        }

        public void Clean()
        {
            var cutoff = DateTime.UtcNow.AddDays(-30); // Keep successes for 30 days
            _indexerSuccessRepository.DeleteOldSuccesses(cutoff);
        }
    }
}

