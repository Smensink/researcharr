using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerSuccessRepository : IBasicRepository<IndexerSuccess>
    {
        List<IndexerSuccess> GetByIndexerId(int indexerId);
        List<IndexerSuccess> GetByIndexerIdAndDateRange(int indexerId, DateTime startDate, DateTime endDate);
        int GetCountByIndexerIdAndOperationType(int indexerId, IndexerOperationType operationType, DateTime? startDate = null);
        void DeleteOldSuccesses(DateTime cutoffDate);
    }
}

