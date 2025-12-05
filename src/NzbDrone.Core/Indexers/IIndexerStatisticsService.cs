using System.Collections.Generic;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerStatisticsService
    {
        List<IndexerStatistics> GetAllStatistics();
        IndexerStatistics GetStatistics(int indexerId);
    }
}

