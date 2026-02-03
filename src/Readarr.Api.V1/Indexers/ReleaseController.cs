using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Validation;
using NzbDrone.SignalR;
using Readarr.Http;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Readarr.Api.V1.Indexers
{
    [V1ApiController]
    public class ReleaseController : ReleaseControllerBase
    {
        private readonly IFetchAndParseRss _rssFetcherAndParser;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IDownloadService _downloadService;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IParsingService _parsingService;
        private readonly IBroadcastSignalRMessage _signalRBroadcast;
        private readonly ISearchSessionService _searchSessionService;
        private readonly Logger _logger;

        private readonly ICached<RemoteBook> _remoteBookCache;

        public ReleaseController(IFetchAndParseRss rssFetcherAndParser,
                             ISearchForReleases releaseSearchService,
                             IMakeDownloadDecision downloadDecisionMaker,
                             IPrioritizeDownloadDecision prioritizeDownloadDecision,
                             IDownloadService downloadService,
                             IAuthorService authorService,
                             IBookService bookService,
                             IParsingService parsingService,
                             IBroadcastSignalRMessage signalRBroadcast,
                             ISearchSessionService searchSessionService,
                             ICacheManager cacheManager,
                             Logger logger)
        {
            _rssFetcherAndParser = rssFetcherAndParser;
            _releaseSearchService = releaseSearchService;
            _downloadDecisionMaker = downloadDecisionMaker;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _downloadService = downloadService;
            _authorService = authorService;
            _bookService = bookService;
            _parsingService = parsingService;
            _signalRBroadcast = signalRBroadcast;
            _searchSessionService = searchSessionService;
            _logger = logger;

            PostValidator.RuleFor(s => s.IndexerId).ValidId();
            PostValidator.RuleFor(s => s.Guid).NotEmpty();

            _remoteBookCache = cacheManager.GetCache<RemoteBook>(GetType(), "remoteBooks");
        }

        [HttpPost]
        public async Task<ActionResult<ReleaseResource>> DownloadRelease(ReleaseResource release)
        {
            ValidateResource(release);

            var remoteBook = _remoteBookCache.Find(GetCacheKey(release));

            if (remoteBook == null)
            {
                _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            try
            {
                if (remoteBook.Author == null)
                {
                    if (release.BookId.HasValue)
                    {
                        var book = _bookService.GetBook(release.BookId.Value);

                        remoteBook.Author = _authorService.GetAuthor(book.AuthorId);
                        remoteBook.Books = new List<Book> { book };
                    }
                    else if (release.AuthorId.HasValue)
                    {
                        var author = _authorService.GetAuthor(release.AuthorId.Value);
                        var books = _parsingService.GetBooks(remoteBook.ParsedBookInfo, author);

                        if (books.Empty())
                        {
                            throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse books in the release");
                        }

                        remoteBook.Author = author;
                        remoteBook.Books = books;
                    }
                    else
                    {
                        throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to find matching author and books");
                    }
                }
                else if (remoteBook.Books.Empty())
                {
                    var books = _parsingService.GetBooks(remoteBook.ParsedBookInfo, remoteBook.Author);

                    if (books.Empty() && release.BookId.HasValue)
                    {
                        var book = _bookService.GetBook(release.BookId.Value);

                        books = new List<Book> { book };
                    }

                    remoteBook.Books = books;
                }

                if (remoteBook.Books.Empty())
                {
                    throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to parse books in the release");
                }

                await _downloadService.DownloadReport(remoteBook, release.DownloadClientId);
            }
            catch (ReleaseDownloadException ex)
            {
                _logger.Error(ex, "Getting release from indexer failed");
                throw new NzbDroneClientException(HttpStatusCode.Conflict, "Getting release from indexer failed");
            }

            return Ok(release);
        }

        [HttpGet]
        public async Task<List<ReleaseResource>> GetReleases(int? bookId, int? authorId)
        {
            if (bookId.HasValue)
            {
                return await GetBookReleases(int.Parse(Request.Query["bookId"]));
            }

            if (authorId.HasValue)
            {
                return await GetAuthorReleases(int.Parse(Request.Query["authorId"]));
            }

            return await GetRss();
        }

        private async Task<List<ReleaseResource>> GetBookReleases(int bookId)
        {
            try
            {
                var decisions = await _releaseSearchService.BookSearch(bookId, true, true, true);
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Book search failed");
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetAuthorReleases(int authorId)
        {
            try
            {
                var decisions = await _releaseSearchService.AuthorSearch(authorId, false, true, true);
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Author search failed");
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetRss()
        {
            var reports = await _rssFetcherAndParser.Fetch();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);

            return MapDecisions(prioritizedDecisions);
        }

        // Streaming search endpoints
        [HttpPost("search")]
        public async Task<ActionResult<SearchSessionResource>> StartStreamingSearch([FromBody] StreamingSearchRequest request)
        {
            if (!request.BookId.HasValue && !request.AuthorId.HasValue)
            {
                return BadRequest("Either bookId or authorId must be provided");
            }

            try
            {
                SearchSession session;

                if (request.BookId.HasValue)
                {
                    session = await _releaseSearchService.BookSearchStreaming(
                        request.BookId.Value,
                        true,
                        true,
                        OnStreamingSearchResults);
                }
                else
                {
                    session = await _releaseSearchService.AuthorSearchStreaming(
                        request.AuthorId.Value,
                        true,
                        true,
                        OnStreamingSearchResults);
                }

                // Broadcast search complete
                await BroadcastSearchComplete(session);

                return Ok(new SearchSessionResource
                {
                    SearchId = session.SearchId,
                    TotalIndexers = session.TotalIndexers,
                    CompletedIndexers = session.CompletedCount,
                    IsComplete = session.IsComplete,
                    TotalResults = session.BestDecisionByGuid.Count
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Streaming search failed");
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet("search/{searchId}")]
        public ActionResult<SearchSessionResource> GetSearchSession(string searchId)
        {
            var session = _searchSessionService.GetSession(searchId);
            if (session == null)
            {
                return NotFound();
            }

            return Ok(new SearchSessionResource
            {
                SearchId = session.SearchId,
                TotalIndexers = session.TotalIndexers,
                CompletedIndexers = session.CompletedCount,
                IsComplete = session.IsComplete,
                TotalResults = session.BestDecisionByGuid.Count,
                IndexerStatuses = session.IndexerStatuses.Values.Select(s => new IndexerSearchStatusResource
                {
                    IndexerId = s.IndexerId,
                    IndexerName = s.IndexerName,
                    State = s.State.ToString().ToLowerInvariant(),
                    ResultCount = s.ResultCount,
                    ErrorMessage = s.ErrorMessage
                }).ToList()
            });
        }

        [HttpGet("search/{searchId}/results")]
        public ActionResult<List<ReleaseResource>> GetSearchResults(string searchId)
        {
            var decisions = _searchSessionService.GetAllDecisions(searchId);
            if (decisions == null)
            {
                return NotFound();
            }

            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisions(decisions);
            return Ok(MapDecisions(prioritizedDecisions));
        }

        private async Task OnStreamingSearchResults(SearchSession session, IIndexer indexer, List<DownloadDecision> decisions)
        {
            // Map decisions to resources and cache them
            var resources = new List<ReleaseResource>();
            var releaseWeight = session.BestDecisionByGuid.Count;

            foreach (var decision in decisions)
            {
                var resource = MapDecision(decision, releaseWeight++);
                resources.Add(resource);
            }

            // Get current indexer status
            var indexerStatus = session.IndexerStatuses.TryGetValue(indexer.Definition.Id, out var status) ? status : null;

            // Broadcast via SignalR
            var message = new SignalRMessage
            {
                Name = "releaseSearch",
                Body = new SearchResultMessage
                {
                    SearchId = session.SearchId,
                    Action = SearchResultMessage.Actions.Results,
                    IndexerId = indexer.Definition.Id,
                    IndexerName = indexer.Definition.Name,
                    Results = resources.Cast<object>().ToList(),
                    TotalIndexers = session.TotalIndexers,
                    CompletedIndexers = session.CompletedCount,
                    IndexerStatuses = session.IndexerStatuses.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new IndexerSearchStatusDto
                        {
                            IndexerId = kvp.Value.IndexerId,
                            IndexerName = kvp.Value.IndexerName,
                            State = kvp.Value.State.ToString().ToLowerInvariant(),
                            ResultCount = kvp.Value.ResultCount,
                            ErrorMessage = kvp.Value.ErrorMessage
                        })
                }
            };

            await _signalRBroadcast.BroadcastMessage(message);
        }

        private async Task BroadcastSearchComplete(SearchSession session)
        {
            var message = new SignalRMessage
            {
                Name = "releaseSearch",
                Body = new SearchResultMessage
                {
                    SearchId = session.SearchId,
                    Action = SearchResultMessage.Actions.Complete,
                    TotalIndexers = session.TotalIndexers,
                    CompletedIndexers = session.CompletedCount,
                    IndexerStatuses = session.IndexerStatuses.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new IndexerSearchStatusDto
                        {
                            IndexerId = kvp.Value.IndexerId,
                            IndexerName = kvp.Value.IndexerName,
                            State = kvp.Value.State.ToString().ToLowerInvariant(),
                            ResultCount = kvp.Value.ResultCount,
                            ErrorMessage = kvp.Value.ErrorMessage
                        })
                }
            };

            await _signalRBroadcast.BroadcastMessage(message);
        }

        protected override ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var resource = base.MapDecision(decision, initialWeight);
            _remoteBookCache.Set(GetCacheKey(resource), decision.RemoteBook, TimeSpan.FromMinutes(30));

            return resource;
        }

        private string GetCacheKey(ReleaseResource resource)
        {
            return string.Concat(resource.IndexerId, "_", resource.Guid);
        }
    }
}
