using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books.Import
{
    public interface IImportSearchJobRepository : IBasicRepository<ImportSearchJob>
    {
    }

    public class ImportSearchJobRepository : BasicRepository<ImportSearchJob>, IImportSearchJobRepository
    {
        public ImportSearchJobRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }

    public interface IImportSearchItemRepository : IBasicRepository<ImportSearchItem>
    {
    }

    public class ImportSearchItemRepository : BasicRepository<ImportSearchItem>, IImportSearchItemRepository
    {
        public ImportSearchItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
