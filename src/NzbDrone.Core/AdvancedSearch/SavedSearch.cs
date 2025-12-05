using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.AdvancedSearch
{
    public class SavedSearch : ModelBase
    {
        public string Name { get; set; }
        public string SearchString { get; set; }
        public string FilterString { get; set; }
        public string SortString { get; set; }
        public string Cursor { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string MeshJson { get; set; }
        public string PubMedQuery { get; set; }
    }
}
