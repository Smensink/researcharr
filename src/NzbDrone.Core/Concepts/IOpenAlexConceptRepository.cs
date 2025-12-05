using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Concepts
{
    public interface IOpenAlexConceptRepository : IBasicRepository<OpenAlexConcept>
    {
        List<OpenAlexConcept> SearchByName(string query, int limit);
    }

    public class OpenAlexConceptRepository : BasicRepository<OpenAlexConcept>, IOpenAlexConceptRepository
    {
        public OpenAlexConceptRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<OpenAlexConcept> SearchByName(string query, int limit)
        {
            // Use raw SQL with LIKE for efficient indexed search
            var sql = $"SELECT * FROM \"{_table}\" WHERE \"DisplayName\" LIKE @query ORDER BY \"WorksCount\" DESC LIMIT @limit";
            return _database.Query<OpenAlexConcept>(sql, new { query = $"%{query}%", limit }).ToList();
        }
    }
}
