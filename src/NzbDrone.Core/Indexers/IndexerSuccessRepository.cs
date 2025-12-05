using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Indexers
{
    public class IndexerSuccessRepository : BasicRepository<IndexerSuccess>, IIndexerSuccessRepository
    {
        public IndexerSuccessRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<IndexerSuccess> GetByIndexerId(int indexerId)
        {
            return Query(x => x.IndexerId == indexerId)
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }

        public List<IndexerSuccess> GetByIndexerIdAndDateRange(int indexerId, DateTime startDate, DateTime endDate)
        {
            return Query(x => x.IndexerId == indexerId && x.Timestamp >= startDate && x.Timestamp <= endDate)
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }

        public int GetCountByIndexerIdAndOperationType(int indexerId, IndexerOperationType operationType, DateTime? startDate = null)
        {
            var query = Query(x => x.IndexerId == indexerId && x.OperationType == operationType);
            
            if (startDate.HasValue)
            {
                return query.Where(x => x.Timestamp >= startDate.Value).Count();
            }
            
            return query.Count();
        }

        public void DeleteOldSuccesses(DateTime cutoffDate)
        {
            var successes = Query(x => x.Timestamp < cutoffDate).ToList();
            if (successes.Any())
            {
                DeleteMany(successes);
            }
        }
    }
}

