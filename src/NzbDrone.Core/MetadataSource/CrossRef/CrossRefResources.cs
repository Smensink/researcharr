using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.CrossRef
{
    public class CrossRefWorkResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("message-type")]
        public string MessageType { get; set; }

        [JsonProperty("message-version")]
        public string MessageVersion { get; set; }

        [JsonProperty("message")]
        public CrossRefWork Message { get; set; }
    }

    public class CrossRefWork
    {
        [JsonProperty("DOI")]
        public string Doi { get; set; }

        [JsonProperty("URL")]
        public string Url { get; set; }

        [JsonProperty("title")]
        public List<string> Title { get; set; }

        [JsonProperty("subtitle")]
        public List<string> Subtitle { get; set; }

        [JsonProperty("abstract")]
        public string Abstract { get; set; }

        [JsonProperty("author")]
        public List<CrossRefAuthor> Authors { get; set; }

        [JsonProperty("container-title")]
        public List<string> ContainerTitle { get; set; } // Journal/Conference name

        [JsonProperty("short-container-title")]
        public List<string> ShortContainerTitle { get; set; }

        [JsonProperty("publisher")]
        public string Publisher { get; set; }

        [JsonProperty("published")]
        public CrossRefDate Published { get; set; }

        [JsonProperty("published-online")]
        public CrossRefDate PublishedOnline { get; set; }

        [JsonProperty("published-print")]
        public CrossRefDate PublishedPrint { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // journal-article, book-chapter, etc.

        [JsonProperty("volume")]
        public string Volume { get; set; }

        [JsonProperty("issue")]
        public string Issue { get; set; }

        [JsonProperty("page")]
        public string Page { get; set; }

        [JsonProperty("is-referenced-by-count")]
        public int ReferencedByCount { get; set; } // Citation count

        [JsonProperty("references-count")]
        public int ReferencesCount { get; set; }

        [JsonProperty("ISBN")]
        public List<string> Isbn { get; set; }

        [JsonProperty("ISSN")]
        public List<string> Issn { get; set; }

        [JsonProperty("subject")]
        public List<string> Subject { get; set; }

        [JsonProperty("link")]
        public List<CrossRefLink> Links { get; set; }

        [JsonProperty("license")]
        public List<CrossRefLicense> Licenses { get; set; }
    }

    public class CrossRefAuthor
    {
        [JsonProperty("given")]
        public string Given { get; set; }

        [JsonProperty("family")]
        public string Family { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } // For organizations

        [JsonProperty("ORCID")]
        public string Orcid { get; set; }

        [JsonProperty("sequence")]
        public string Sequence { get; set; } // first, additional

        [JsonProperty("affiliation")]
        public List<CrossRefAffiliation> Affiliations { get; set; }
    }

    public class CrossRefAffiliation
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class CrossRefDate
    {
        [JsonProperty("date-parts")]
        public List<List<int>> DateParts { get; set; }

        [JsonProperty("date-time")]
        public DateTime? DateTime { get; set; }

        [JsonProperty("timestamp")]
        public long? Timestamp { get; set; }
    }

    public class CrossRefLink
    {
        [JsonProperty("URL")]
        public string Url { get; set; }

        [JsonProperty("content-type")]
        public string ContentType { get; set; }

        [JsonProperty("content-version")]
        public string ContentVersion { get; set; }

        [JsonProperty("intended-application")]
        public string IntendedApplication { get; set; }
    }

    public class CrossRefLicense
    {
        [JsonProperty("URL")]
        public string Url { get; set; }

        [JsonProperty("start")]
        public CrossRefDate Start { get; set; }

        [JsonProperty("delay-in-days")]
        public int DelayInDays { get; set; }

        [JsonProperty("content-version")]
        public string ContentVersion { get; set; }
    }
}
