using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchForReleases
    {
        Task<List<DownloadDecision>> BookSearch(int bookId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> AuthorSearch(int authorId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
    }

    public class ReleaseSearchService : ISearchForReleases
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly Logger _logger;

        public ReleaseSearchService(IIndexerFactory indexerFactory,
                                    IIndexerStatusService indexerStatusService,
                                IBookService bookService,
                                IAuthorService authorService,
                                IMakeDownloadDecision makeDownloadDecision,
                                Logger logger)
        {
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _bookService = bookService;
            _authorService = authorService;
            _makeDownloadDecision = makeDownloadDecision;
            _logger = logger;
        }

        public async Task<List<DownloadDecision>> BookSearch(int bookId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var book = _bookService.GetBook(bookId);

            var decisions = await BookSearch(book, missingOnly, userInvokedSearch, interactiveSearch);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        public async Task<List<DownloadDecision>> AuthorSearch(int authorId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var author = _authorService.GetAuthor(authorId);

            var decisions = await AuthorSearch(author, missingOnly, userInvokedSearch, interactiveSearch);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        public async Task<List<DownloadDecision>> AuthorSearch(Author author, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var searchSpec = Get<AuthorSearchCriteria>(author, userInvokedSearch, interactiveSearch);
            var books = _bookService.GetBooksByAuthor(author.Id);

            books = books.Where(a => a.Monitored).ToList();

            searchSpec.Books = books;

            return await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        public async Task<List<DownloadDecision>> BookSearch(Book book, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var author = _authorService.GetAuthor(book.AuthorId);

            var searchSpec = Get<BookSearchCriteria>(author, new List<Book> { book }, userInvokedSearch, interactiveSearch);

            var monitoredEdition = book.Editions.Value.SingleOrDefault(x => x.Monitored);
            searchSpec.BookTitle = monitoredEdition?.Title ?? book.Title;

            // Extract DOI from book - most important identifier for academic papers
            // Check Links first (where DOI is stored as Name="DOI")
            var doiLink = book.Links?.FirstOrDefault(l => l.Name?.Equals("DOI", StringComparison.OrdinalIgnoreCase) == true);
            if (doiLink != null && !string.IsNullOrWhiteSpace(doiLink.Url))
            {
                searchSpec.BookDoi = Parser.DoiUtility.Normalize(doiLink.Url);
            }
            else if (monitoredEdition != null && !string.IsNullOrWhiteSpace(monitoredEdition.Isbn13))
            {
                // Fallback to Edition.Isbn13 (which may contain DOI for academic papers)
                var normalizedDoi = Parser.DoiUtility.Normalize(monitoredEdition.Isbn13);
                if (normalizedDoi != null)
                {
                    searchSpec.BookDoi = normalizedDoi;
                }
                else
                {
                    searchSpec.BookIsbn = monitoredEdition.Isbn13;
                }
            }

            if (monitoredEdition != null)
            {
                searchSpec.Disambiguation = monitoredEdition.Disambiguation;
            }

            if (book.ReleaseDate.HasValue)
            {
                searchSpec.BookYear = book.ReleaseDate.Value.Year;
            }

            return await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private TSpec Get<TSpec>(Author author, List<Book> books, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Books = books;
            spec.Author = author;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private static TSpec Get<TSpec>(Author author, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();
            spec.Author = author;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private async Task<List<DownloadDecision>> Dispatch(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, SearchCriteriaBase criteriaBase)
        {
            var indexers = criteriaBase.InteractiveSearch ?
                _indexerFactory.InteractiveSearchEnabled() :
                _indexerFactory.AutomaticSearchEnabled();

            // Filter indexers to untagged indexers and indexers with intersecting tags
            indexers = indexers.Where(i => i.Definition.Tags.Empty() || i.Definition.Tags.Intersect(criteriaBase.Author.Tags).Any()).ToList();

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", criteriaBase, indexers.Count);

            var tasks = indexers.Select(indexer => DispatchIndexer(searchAction, indexer, criteriaBase));

            var batch = await Task.WhenAll(tasks);

            var reports = batch.SelectMany(x => x).ToList();

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", reports.Count, criteriaBase, indexers.Count);

            // Update the last search time for all albums if at least 1 indexer was searched.
            if (indexers.Any())
            {
                var lastSearchTime = DateTime.UtcNow;
                _logger.Debug("Setting last search time to: {0}", lastSearchTime);

                criteriaBase.Books.ForEach(a => a.LastSearchTime = lastSearchTime);
                _bookService.UpdateLastSearchTime(criteriaBase.Books);
            }

            return _makeDownloadDecision.GetSearchDecision(reports, criteriaBase).ToList();
        }

        private async Task<IList<ReleaseInfo>> DispatchIndexer(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, IIndexer indexer, SearchCriteriaBase criteriaBase)
        {
            try
            {
                var results = await searchAction(indexer);
                
                // Record success for search operation
                try
                {
                    _indexerStatusService.RecordSuccess(indexer.Definition.Id, IndexerOperationType.Search);
                }
                catch
                {
                    // Ignore errors in success recording
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while searching for {0}", criteriaBase);
                
                // Record detailed failure information
                try
                {
                    var errorType = DetermineErrorType(ex);
                    var httpStatusCode = GetHttpStatusCode(ex);
                    _indexerStatusService.RecordFailure(indexer.Definition.Id, IndexerOperationType.Search, errorType, ex.Message, httpStatusCode);
                }
                catch
                {
                    // Ignore errors in failure recording
                }
            }

            return Array.Empty<ReleaseInfo>();
        }

        private static IndexerErrorType DetermineErrorType(Exception ex)
        {
            return ex switch
            {
                System.Net.Http.HttpRequestException => IndexerErrorType.ConnectionFailure,
                System.Net.WebException webEx when webEx.Status == System.Net.WebExceptionStatus.Timeout => IndexerErrorType.Timeout,
                System.Net.WebException => IndexerErrorType.ConnectionFailure,
                System.Threading.Tasks.TaskCanceledException => IndexerErrorType.Timeout,
                NzbDrone.Common.Http.HttpException httpEx when httpEx.Response?.StatusCode == System.Net.HttpStatusCode.Unauthorized || httpEx.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden => IndexerErrorType.AuthError,
                NzbDrone.Common.Http.HttpException => IndexerErrorType.HttpError,
                NzbDrone.Core.Indexers.Exceptions.RequestLimitReachedException => IndexerErrorType.RateLimit,
                NzbDrone.Core.Http.CloudFlare.CloudFlareCaptchaException => IndexerErrorType.CloudflareCaptcha,
                _ => IndexerErrorType.Unknown
            };
        }

        private static int? GetHttpStatusCode(Exception ex)
        {
            if (ex is NzbDrone.Common.Http.HttpException httpEx)
            {
                return (int?)httpEx.Response?.StatusCode;
            }

            return null;
        }

        private List<DownloadDecision> DeDupeDecisions(List<DownloadDecision> decisions)
        {
            // De-dupe reports by guid so duplicate results aren't returned. Pick the one with the least rejections and higher indexer priority.
            return decisions.GroupBy(d => d.RemoteBook.Release.Guid)
                .Select(d => d.OrderBy(v => v.Rejections.Count()).ThenBy(v => v.RemoteBook?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority).First())
                .ToList();
        }
    }
}
