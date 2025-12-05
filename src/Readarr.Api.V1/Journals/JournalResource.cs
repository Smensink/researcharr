using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;

namespace Readarr.Api.V1.Journals
{
    public class JournalResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TitleSlug { get; set; }
        public int PaperCount { get; set; }
        public int MonitoredPaperCount { get; set; }
        public bool Monitored { get; set; }
    }

    public static class JournalResourceMapper
    {
        public static JournalResource ToJournalResource(this NzbDrone.Core.Books.Author journal, Dictionary<int, AuthorStatistics> authorStats)
        {
            if (journal == null)
            {
                return null;
            }

            var stats = authorStats.ContainsKey(journal.Id) ? authorStats[journal.Id] : null;

            // Ensure TitleSlug is never null or empty - required by frontend
            var titleSlug = journal.Metadata?.Value?.TitleSlug;
            if (string.IsNullOrWhiteSpace(titleSlug))
            {
                titleSlug = journal.Metadata?.Value?.ForeignAuthorId;
            }
            if (string.IsNullOrWhiteSpace(titleSlug))
            {
                titleSlug = journal.Id > 0 ? journal.Id.ToString() : "unknown";
            }

            // Ensure Monitored has a default value
            var monitored = journal.Monitored;

            return new JournalResource
            {
                Id = journal.Id,
                Name = journal.Metadata?.Value?.Name ?? "Unknown Journal",
                TitleSlug = titleSlug,
                PaperCount = stats?.BookCount ?? 0,
                MonitoredPaperCount = stats?.BookCount ?? 0,
                Monitored = monitored
            };
        }
    }
}
