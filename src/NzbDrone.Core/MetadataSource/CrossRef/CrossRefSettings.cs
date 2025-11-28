namespace NzbDrone.Core.MetadataSource.CrossRef
{
    public class CrossRefSettings
    {
        public string BaseUrl { get; set; } = "https://api.crossref.org";
        public string UserAgent { get; set; } = "Researcharr/1.0 (https://github.com/Researcharr/Researcharr; mailto:researcharr@example.com)";
        public string MailTo { get; set; } = "researcharr@example.com"; // For polite pool (faster responses)
    }
}
