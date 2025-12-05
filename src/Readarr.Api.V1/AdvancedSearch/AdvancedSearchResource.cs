using System;
using System.Collections.Generic;

namespace Readarr.Api.V1.AdvancedSearch
{
    public class AdvancedSearchWorkResource
    {
        public string OpenAlexId { get; set; }
        public string Title { get; set; }
        public int? Year { get; set; }
        public string Journal { get; set; }
        public string Doi { get; set; }
        public bool IsOpenAccess { get; set; }
        public string OpenAccessUrl { get; set; }
        public int? CitedByCount { get; set; }
        public List<string> Authors { get; set; }
    }

    public class AdvancedSearchResponseResource
    {
        public List<AdvancedSearchWorkResource> Results { get; set; }
        public string NextCursor { get; set; }
    }

    public class SavedSearchResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SearchString { get; set; }
        public string FilterString { get; set; }
        public string SortString { get; set; }
        public string Cursor { get; set; }
        public List<MeshSelectionResource> MeshSelections { get; set; }
        public string PubMedQuery { get; set; }
    }

    public class MeshSelectionResource
    {
        public string DescriptorUi { get; set; }
        public string PreferredTerm { get; set; }
        public List<string> Synonyms { get; set; }
    }

    public class AdvancedSearchAddRequest
    {
        public string OpenAlexId { get; set; }
        public string Doi { get; set; }
    }

    public class AdvancedSearchConceptResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class BookStatusResource
    {
        public string OpenAlexId { get; set; }
        public int? BookId { get; set; }
        public bool IsMonitored { get; set; }
        public bool HasFile { get; set; }
        public DateTime? LastHistoryDate { get; set; }
        public string LastHistoryEventType { get; set; }
    }
}
