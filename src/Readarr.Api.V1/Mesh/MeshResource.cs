using System.Collections.Generic;

namespace Readarr.Api.V1.Mesh
{
    public class MeshDescriptorResource
    {
        public string DescriptorUi { get; set; }
        public string PreferredTerm { get; set; }
        public List<string> TreeNumbers { get; set; }
        public string ScopeNote { get; set; }
        public List<string> Synonyms { get; set; }
    }

    public class MeshImportResource
    {
        public int Descriptors { get; set; }
        public int Terms { get; set; }
        public string Version { get; set; }
        public string SourceUrl { get; set; }
    }
}
