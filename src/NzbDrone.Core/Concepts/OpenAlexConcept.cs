using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Concepts
{
    public class OpenAlexConcept : ModelBase
    {
        public string OpenAlexId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int CitedByCount { get; set; }
        public int WorksCount { get; set; }
    }
}
