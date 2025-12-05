using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Mesh
{
    public interface IMeshMetadataRepository : IBasicRepository<MeshMetadata>
    {
    }

    public class MeshMetadataRepository : BasicRepository<MeshMetadata>, IMeshMetadataRepository
    {
        public MeshMetadataRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
