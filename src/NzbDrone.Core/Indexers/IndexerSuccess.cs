using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Indexers
{
    public class IndexerSuccess : ModelBase
    {
        public int IndexerId { get; set; }
        public IndexerOperationType OperationType { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

