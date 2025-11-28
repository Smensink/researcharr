using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.AuthorStats;
using NzbDrone.Core.Books;
using Readarr.Http;

namespace Readarr.Api.V1.Journals
{
    [V1ApiController]
    public class JournalController : Controller
    {
        private readonly IAuthorService _authorService;
        private readonly IAuthorStatisticsService _authorStatisticsService;

        public JournalController(IAuthorService authorService, IAuthorStatisticsService authorStatisticsService)
        {
            _authorService = authorService;
            _authorStatisticsService = authorStatisticsService;
        }

        [HttpGet]
        public List<JournalResource> GetJournals()
        {
            // Get all authors where Type == Journal
            var journals = _authorService.GetAllAuthors()
                .Where(a => a.Metadata.Value.Type == AuthorMetadataType.Journal)
                .ToList();

            var authorStats = _authorStatisticsService.AuthorStatistics()
                .ToDictionary(x => x.AuthorId);

            // Map to journal resources with statistics
            var journalResources = journals.Select(j => j.ToJournalResource(authorStats)).ToList();

            return journalResources;
        }
    }
}
