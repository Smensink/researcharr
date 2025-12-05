using System;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Concepts
{
    public class ConceptResource : RestResource
    {
        public string OpenAlexId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int WorksCount { get; set; }
    }

    public class ConceptImportResource
    {
        public int Concepts { get; set; }
        public DateTime ImportedAt { get; set; }
    }
}
