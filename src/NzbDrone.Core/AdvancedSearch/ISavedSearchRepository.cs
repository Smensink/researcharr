using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.AdvancedSearch
{
    public interface ISavedSearchRepository : IBasicRepository<SavedSearch>
    {
    }

    public class SavedSearchRepository : BasicRepository<SavedSearch>, ISavedSearchRepository
    {
        public SavedSearchRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
