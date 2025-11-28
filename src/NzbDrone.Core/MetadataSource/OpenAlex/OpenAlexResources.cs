using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenAlex
{
    public class OpenAlexListResponse<T>
    {
        [JsonProperty("meta")]
        public Meta Meta { get; set; }

        [JsonProperty("results")]
        public List<T> Results { get; set; }
    }

    public class Meta
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("per_page")]
        public int PerPage { get; set; }
    }

    public class OpenAlexAuthor
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("works_count")]
        public int WorksCount { get; set; }

        [JsonProperty("cited_by_count")]
        public int CitedByCount { get; set; }

        [JsonProperty("ids")]
        public OpenAlexIds Ids { get; set; }

        [JsonProperty("last_known_institutions")]
        public List<Institution> LastKnownInstitutions { get; set; }
    }

    public class OpenAlexWork
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("publication_date")]
        public string PublicationDate { get; set; }

        [JsonProperty("publication_year")]
        public int? PublicationYear { get; set; }

        [JsonProperty("cited_by_count")]
        public int CitedByCount { get; set; }

        [JsonProperty("ids")]
        public OpenAlexIds Ids { get; set; }

        [JsonProperty("authorships")]
        public List<Authorship> Authorships { get; set; }

        [JsonProperty("primary_location")]
        public Location PrimaryLocation { get; set; }

        [JsonProperty("open_access")]
        public OpenAccess OpenAccess { get; set; }
    }

    public class OpenAlexIds
    {
        [JsonProperty("openalex")]
        public string OpenAlex { get; set; }

        [JsonProperty("doi")]
        public string Doi { get; set; }

        [JsonProperty("mag")]
        public string Mag { get; set; }

        [JsonProperty("pmid")]
        public string Pmid { get; set; }
    }

    public class Authorship
    {
        [JsonProperty("author")]
        public OpenAlexAuthor Author { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }
    }

    public class Location
    {
        [JsonProperty("pdf_url")]
        public string PdfUrl { get; set; }

        [JsonProperty("landing_page_url")]
        public string LandingPageUrl { get; set; }

        [JsonProperty("is_oa")]
        public bool IsOa { get; set; }

        [JsonProperty("raw_source_name")]
        public string RawSourceName { get; set; }

        [JsonProperty("source")]
        public Source Source { get; set; }
    }

    public class OpenAccess
    {
        [JsonProperty("is_oa")]
        public bool IsOa { get; set; }

        [JsonProperty("oa_url")]
        public string OaUrl { get; set; }
    }

    public class Source
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }

    public class Institution
    {
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }

    public class OpenAlexSource
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("works_count")]
        public int WorksCount { get; set; }

        [JsonProperty("cited_by_count")]
        public int CitedByCount { get; set; }
    }

    public class OpenAlexTopic
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("works_api_url")]
        public string WorksApiUrl { get; set; }
    }
}
