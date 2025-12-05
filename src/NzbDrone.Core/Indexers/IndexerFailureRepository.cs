using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Indexers
{
    public class IndexerFailureRepository : BasicRepository<IndexerFailure>, IIndexerFailureRepository
    {
        public IndexerFailureRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<IndexerFailure> GetByIndexerId(int indexerId)
        {
            return Query(x => x.IndexerId == indexerId)
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }

        public List<IndexerFailure> GetByIndexerIdAndDateRange(int indexerId, DateTime startDate, DateTime endDate)
        {
            return Query(x => x.IndexerId == indexerId && x.Timestamp >= startDate && x.Timestamp <= endDate)
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }

        public void DeleteOldFailures(DateTime cutoffDate)
        {
            var failures = Query(x => x.Timestamp < cutoffDate).ToList();
            if (failures.Any())
            {
                DeleteMany(failures);
            }
        }
    }
}

