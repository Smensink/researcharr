using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Mesh
{
    public interface IMeshDescriptorRepository : IBasicRepository<MeshDescriptor>
    {
    }

    public interface IMeshTermRepository : IBasicRepository<MeshTerm>
    {
        List<MeshTerm> FindByDescriptor(string descriptorUi);
        List<MeshTerm> SearchByTerm(string query, int limit);
    }

    public class MeshDescriptorRepository : BasicRepository<MeshDescriptor>, IMeshDescriptorRepository
    {
        public MeshDescriptorRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }

    public class MeshTermRepository : BasicRepository<MeshTerm>, IMeshTermRepository
    {
        public MeshTermRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<MeshTerm> FindByDescriptor(string descriptorUi)
        {
            return Query(t => t.DescriptorUi == descriptorUi);
        }

        public List<MeshTerm> SearchByTerm(string query, int limit)
        {
            // Use raw SQL with LIKE for efficient indexed search
            var sql = $"SELECT * FROM \"{_table}\" WHERE \"Term\" LIKE @query LIMIT @limit";
            return _database.Query<MeshTerm>(sql, new { query = $"%{query}%", limit }).ToList();
        }
    }
}
