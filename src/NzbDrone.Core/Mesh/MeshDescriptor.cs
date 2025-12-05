using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Mesh
{
    public class MeshDescriptor : ModelBase
    {
        public string DescriptorUi { get; set; }
        public string PreferredTerm { get; set; }
        public string TreeNumbers { get; set; }
        public string ScopeNote { get; set; }
    }

    public class MeshTerm : ModelBase
    {
        public string DescriptorUi { get; set; }
        public string Term { get; set; }
        public bool IsPreferred { get; set; }
    }
}
