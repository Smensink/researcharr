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
            var stats = authorStats.ContainsKey(journal.Id) ? authorStats[journal.Id] : null;

            return new JournalResource
            {
                Id = journal.Id,
                Name = journal.Metadata.Value.Name,
                TitleSlug = journal.Metadata.Value.TitleSlug,
                PaperCount = stats?.BookCount ?? 0,
                MonitoredPaperCount = stats?.BookCount ?? 0,
                Monitored = journal.Monitored
            };
        }
    }
}
