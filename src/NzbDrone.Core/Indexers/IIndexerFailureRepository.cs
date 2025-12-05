using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerFailureRepository : IBasicRepository<IndexerFailure>
    {
        List<IndexerFailure> GetByIndexerId(int indexerId);
        List<IndexerFailure> GetByIndexerIdAndDateRange(int indexerId, DateTime startDate, DateTime endDate);
        void DeleteOldFailures(DateTime cutoffDate);
    }
}

