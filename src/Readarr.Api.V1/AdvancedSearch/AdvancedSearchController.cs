using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.AdvancedSearch;
using NzbDrone.Core.Books;
using NzbDrone.Core.Concepts;
using NzbDrone.Core.History;
using NzbDrone.Common.Http;
using NzbDrone.Core.MetadataSource.OpenAlex;
using Readarr.Http;

namespace Readarr.Api.V1.AdvancedSearch
{
    [V1ApiController]
    [Route("/api/v1/advancedsearch")]
    public class AdvancedSearchController : Controller
    {
        private readonly IAdvancedSearchService _advancedSearchService;
        private readonly IOpenAlexProxy _openAlexProxy;
        private readonly IOpenAlexConceptService _conceptService;
        private readonly IBookService _bookService;
        private readonly IHistoryService _historyService;

        public AdvancedSearchController(IAdvancedSearchService advancedSearchService,
                                        IOpenAlexProxy openAlexProxy,
                                        IOpenAlexConceptService conceptService,
                                        IBookService bookService,
                                        IHistoryService historyService)
        {
            _advancedSearchService = advancedSearchService;
            _openAlexProxy = openAlexProxy;
            _conceptService = conceptService;
            _bookService = bookService;
            _historyService = historyService;
        }

        [HttpGet("works")]
        public async Task<AdvancedSearchResponseResource> Works([FromQuery] string search, [FromQuery] string filter, [FromQuery] string sort, [FromQuery] int perPage = 25, [FromQuery] string cursor = null)
        {
            try
            {
                var response = await _advancedSearchService.SearchAsync(search, filter, sort, perPage, cursor);

                return new AdvancedSearchResponseResource
                {
                    Results = response.Results?.Select(Map).ToList() ?? new List<AdvancedSearchWorkResource>(),
                    NextCursor = response.Meta?.NextCursor
                };
            }
            catch (HttpException)
            {
                // Swallow OpenAlex failures and return empty set to avoid 500s on the UI
                return new AdvancedSearchResponseResource
                {
                    Results = new List<AdvancedSearchWorkResource>(),
                    NextCursor = null
                };
            }
        }

        [HttpPost("add")]
        public async Task Add([FromBody] AdvancedSearchAddRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.OpenAlexId))
            {
                await _advancedSearchService.AddFromOpenAlexIdAsync(request.OpenAlexId);
            }
            else if (!string.IsNullOrWhiteSpace(request.Doi))
            {
                await _advancedSearchService.AddFromDoiAsync(request.Doi);
            }
        }

        [HttpPost("bulkadd")]
        public async Task BulkAdd([FromBody] List<AdvancedSearchAddRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return;
            }

            foreach (var req in requests)
            {
                if (req == null)
                {
                    continue;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(req.OpenAlexId))
                    {
                        // Normalize OpenAlex ID (remove https://openalex.org/ prefix if present)
                        var openAlexId = req.OpenAlexId;
                        if (openAlexId.StartsWith("https://openalex.org/"))
                        {
                            openAlexId = openAlexId.Replace("https://openalex.org/", "");
                        }
                        await _advancedSearchService.AddFromOpenAlexIdAsync(openAlexId);
                    }
                    else if (!string.IsNullOrWhiteSpace(req.Doi))
                    {
                        await _advancedSearchService.AddFromDoiAsync(req.Doi);
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue processing other items
                    // Individual failures shouldn't stop the entire bulk operation
                    Debug.WriteLine($"Failed to add book: {req?.OpenAlexId ?? req?.Doi ?? "unknown"}, Error: {ex.Message}");
                }
            }
        }

        [HttpGet("concepts")]
        public async Task<List<AdvancedSearchConceptResource>> Concepts(string q)
        {
            // Prefer locally imported concepts for fast autocomplete
            var concepts = _conceptService.SearchConcepts(q, 10);

            // Fallback to OpenAlex live search if local cache is empty
            if (concepts == null || concepts.Count == 0)
            {
                var remote = await _openAlexProxy.SearchConceptsAsync(q, 10);
                return remote.Select(c => new AdvancedSearchConceptResource
                {
                    Id = c.Id,
                    Name = c.DisplayName
                }).ToList();
            }

            return concepts.Select(c => new AdvancedSearchConceptResource
            {
                Id = $"https://openalex.org/{c.OpenAlexId}",
                Name = c.DisplayName
            }).ToList();
        }

        [HttpGet("saved")]
        public List<SavedSearchResource> GetSaved()
        {
            return _advancedSearchService.All().Select(Map).ToList();
        }

        [HttpPost("saved")]
        public SavedSearchResource Save([FromBody] SavedSearchResource resource)
        {
            var model = new SavedSearch
            {
                Name = resource.Name,
                SearchString = resource.SearchString,
                FilterString = resource.FilterString,
                SortString = resource.SortString,
                Cursor = resource.Cursor,
                MeshJson = resource.MeshSelections != null ? JsonConvert.SerializeObject(resource.MeshSelections) : null,
                PubMedQuery = AdvancedSearchService.BuildPubMedQueryFromMeshJson(resource.MeshSelections != null ? JsonConvert.SerializeObject(resource.MeshSelections) : null)
            };

            var created = _advancedSearchService.Create(model);
            return Map(created);
        }

        [HttpDelete("saved/{id:int}")]
        public void DeleteSaved(int id)
        {
            _advancedSearchService.Delete(id);
        }

        [HttpPost("status")]
        public List<BookStatusResource> GetStatuses([FromBody] List<string> openAlexIds)
        {
            if (openAlexIds == null || !openAlexIds.Any())
            {
                return new List<BookStatusResource>();
            }

            var statuses = new List<BookStatusResource>();

            foreach (var openAlexId in openAlexIds)
            {
                // Normalize OpenAlex ID (remove https://openalex.org/ prefix if present)
                var normalizedId = openAlexId;
                if (normalizedId.StartsWith("https://openalex.org/"))
                {
                    normalizedId = normalizedId.Replace("https://openalex.org/", "");
                }
                // OpenAlex IDs are stored as W123456789 format in ForeignBookId
                // The work ID from the API is in format https://openalex.org/W123456789
                // So we need to extract just the W123456789 part

                var book = _bookService.FindById(normalizedId);
                var status = new BookStatusResource
                {
                    OpenAlexId = openAlexId,
                    BookId = book?.Id,
                    IsMonitored = book?.Monitored ?? false,
                    HasFile = book != null && book.BookFiles?.Value != null && book.BookFiles.Value.Any()
                };

                if (book != null)
                {
                    var mostRecentHistory = _historyService.MostRecentForBook(book.Id);
                    if (mostRecentHistory != null)
                    {
                        status.LastHistoryDate = mostRecentHistory.Date;
                        status.LastHistoryEventType = mostRecentHistory.EventType.ToString();
                    }
                }

                statuses.Add(status);
            }

            return statuses;
        }

        private static AdvancedSearchWorkResource Map(OpenAlexWork work)
        {
            return new AdvancedSearchWorkResource
            {
                OpenAlexId = work.Id,
                Title = work.DisplayName,
                Year = work.PublicationYear,
                Journal = work.PrimaryLocation?.RawSourceName ?? work.PrimaryLocation?.Source?.DisplayName,
                Doi = work.Ids?.Doi,
                IsOpenAccess = work.OpenAccess?.IsOa ?? work.PrimaryLocation?.IsOa ?? false,
                OpenAccessUrl = work.PrimaryLocation?.PdfUrl ?? work.OpenAccess?.OaUrl ?? work.PrimaryLocation?.LandingPageUrl,
                CitedByCount = work.CitedByCount,
                Authors = work.Authorships?.Select(a => a.Author?.DisplayName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>()
            };
        }

        private static SavedSearchResource Map(SavedSearch search)
        {
            return new SavedSearchResource
            {
                Id = search.Id,
                Name = search.Name,
                SearchString = search.SearchString,
                FilterString = search.FilterString,
                SortString = search.SortString,
                Cursor = search.Cursor,
                MeshSelections = !string.IsNullOrWhiteSpace(search.MeshJson)
                    ? JsonConvert.DeserializeObject<List<MeshSelectionResource>>(search.MeshJson)
                    : null
            };
        }
    }
}
