using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Mesh
{
    public class MeshMetadata : ModelBase
    {
        public string SourceUrl { get; set; }
        public string Version { get; set; }
        public DateTime ImportedAt { get; set; }
    }
}
