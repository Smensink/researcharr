using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly IEnumerable<IDecisionEngineSpecification> _specifications;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IParsingService _parsingService;
        private readonly IRemoteBookAggregationService _aggregationService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IDecisionEngineSpecification> specifications,
            IParsingService parsingService,
            ICustomFormatCalculationService formatService,
            IRemoteBookAggregationService aggregationService,
            Logger logger)
        {
            _specifications = specifications;
            _parsingService = parsingService;
            _formatCalculator = formatService;
            _aggregationService = aggregationService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetBookDecisions(reports).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase)
        {
            return GetBookDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetBookDecisions(List<ReleaseInfo> reports, bool pushedRelease = false, SearchCriteriaBase searchCriteria = null)
        {
            if (reports.Any())
            {
                _logger.ProgressInfo("Processing {0} releases", reports.Count);
            }
            else
            {
                _logger.ProgressInfo("No results found");
            }

            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                var remoteBook = default(RemoteBook);

                try
                {
                    var parsedBookInfo = Parser.Parser.ParseBookTitle(report.Title);

                    if (parsedBookInfo == null)
                    {
                        if (searchCriteria != null)
                        {
                            parsedBookInfo = Parser.Parser.ParseBookTitleWithSearchCriteria(report.Title,
                                                                                              searchCriteria.Author,
                                                                                              searchCriteria.Books);
                        }
                        else
                        {
                            // try parsing fuzzy
                            parsedBookInfo = _parsingService.ParseBookTitleFuzzy(report.Title);
                        }
                    }

                    // Normalize DOI up front
                    report.Doi = DoiUtility.Normalize(report.Doi);

                    // If parsing failed but we have Author/Book fields directly from the indexer, use those
                    if (parsedBookInfo == null && report.Author.IsNotNullOrWhiteSpace() && report.Book.IsNotNullOrWhiteSpace())
                    {
                        _logger.Debug("Using indexer-provided Author/Book fields for {0}", report.Title);
                        parsedBookInfo = new ParsedBookInfo
                        {
                            AuthorName = report.Author,
                            BookTitle = report.Book,
                            Quality = new QualityModel(Quality.PDF)
                        };
                    }

                    if (searchCriteria != null && parsedBookInfo != null)
                    {
                        // Check for DOI match
                        var doiMatch = DoiUtility.IsDoiMatch(report, searchCriteria);

                        // Fill in blanks from indexer-provided fields or search criteria
                        if (parsedBookInfo.AuthorName.IsNullOrWhiteSpace() ||
                            parsedBookInfo.AuthorName.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            // Try indexer-provided author first
                            if (report.Author.IsNotNullOrWhiteSpace())
                            {
                                parsedBookInfo.AuthorName = report.Author;
                            }
                            else if (doiMatch && searchCriteria.Author != null)
                            {
                                // Fall back to search criteria if DOI matches
                                parsedBookInfo.AuthorName = searchCriteria.Author.Name;
                            }
                        }

                        if (parsedBookInfo.BookTitle.IsNullOrWhiteSpace())
                        {
                            // Try indexer-provided book title first
                            if (report.Book.IsNotNullOrWhiteSpace())
                            {
                                parsedBookInfo.BookTitle = report.Book;
                            }
                            else if (doiMatch && searchCriteria.Books != null && searchCriteria.Books.Any())
                            {
                                // Fall back to search criteria if DOI matches
                                parsedBookInfo.BookTitle = searchCriteria.Books.First().Title;
                            }
                        }

                        // Fallback for non-DOI but trusted/specific searches could go here, but user asked for strictness.
                        // If we don't fill in the blanks, parsedBookInfo remains as is (likely null author/title for weird filenames).
                        if (parsedBookInfo.Quality == null || parsedBookInfo.Quality.Quality == Quality.Unknown)
                        {
                            parsedBookInfo.Quality = new QualityModel(Quality.PDF);
                        }
                    }

                    if (parsedBookInfo != null && !parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
                    {
                        remoteBook = _parsingService.Map(parsedBookInfo, searchCriteria);
                        remoteBook.Release = report;

                        _aggregationService.Augment(remoteBook);

                        // try parsing again using the search criteria, in case it parsed but parsed incorrectly
                        if ((remoteBook.Author == null || remoteBook.Books.Empty()) && searchCriteria != null)
                        {
                            _logger.Debug("Author/Book null for {0}, reparsing with search criteria", report.Title);
                            var parsedBookInfoWithCriteria = Parser.Parser.ParseBookTitleWithSearchCriteria(report.Title,
                                                                                                                searchCriteria.Author,
                                                                                                                searchCriteria.Books);

                            if (parsedBookInfoWithCriteria != null && parsedBookInfoWithCriteria.AuthorName.IsNotNullOrWhiteSpace())
                            {
                                remoteBook = _parsingService.Map(parsedBookInfoWithCriteria, searchCriteria);
                            }
                        }

                        remoteBook.Release = report;

                        // parse quality again with title and category if unknown
                        if (remoteBook.ParsedBookInfo.Quality.Quality == Quality.Unknown)
                        {
                            remoteBook.ParsedBookInfo.Quality = QualityParser.ParseQuality(report.Title, null, report.Categories);
                        }

                        if (remoteBook.Author == null)
                        {
                            // If author is unknown but DOI matches, we can trust the release and use the search criteria author
                            if (DoiUtility.IsDoiMatch(report, searchCriteria) && searchCriteria?.Author != null)
                            {
                                _logger.Debug("Author unknown but DOI matches, using search criteria author");
                                remoteBook.Author = searchCriteria.Author;

                                if (remoteBook.Books.Empty() && searchCriteria.Books != null)
                                {
                                    remoteBook.Books = searchCriteria.Books;
                                }
                            }
                            else
                            {
                                decision = new DownloadDecision(remoteBook, new Rejection("Unknown Author"));
                            }
                        }
                        else if (remoteBook.Books.Empty())
                        {
                            // If Books is empty but DOI matches, populate from search criteria
                            if (DoiUtility.IsDoiMatch(report, searchCriteria) && searchCriteria?.Books != null)
                            {
                                _logger.Debug("Books empty but DOI matches, using search criteria books");
                                remoteBook.Books = searchCriteria.Books;
                            }

                            // Only reject if Books is still empty after trying to populate
                            if (remoteBook.Books.Empty())
                            {
                                decision = new DownloadDecision(remoteBook, new Rejection("Unable to parse books from release name"));
                            }
                        }
                        else
                        {
                            _aggregationService.Augment(remoteBook);

                            remoteBook.CustomFormats = _formatCalculator.ParseCustomFormat(remoteBook, remoteBook.Release.Size);
                            remoteBook.CustomFormatScore = remoteBook?.Author?.QualityProfile?.Value.CalculateCustomFormatScore(remoteBook.CustomFormats) ?? 0;

                            remoteBook.DownloadAllowed = remoteBook.Books.Any();
                            decision = GetDecisionForReport(remoteBook, searchCriteria);
                        }
                    }

                    if (searchCriteria != null)
                    {
                        if (parsedBookInfo == null)
                        {
                            parsedBookInfo = new ParsedBookInfo
                            {
                                Quality = QualityParser.ParseQuality(report.Title, null, report.Categories)
                            };
                        }

                        // Only fill from search criteria if DOI matches (DOI is a strong identifier)
                        var doiMatchForParsedInfo = DoiUtility.IsDoiMatch(report, searchCriteria);

                        if (parsedBookInfo.AuthorName.IsNullOrWhiteSpace() && searchCriteria.Author != null && doiMatchForParsedInfo)
                        {
                            _logger.Debug("Author name empty but DOI matches, using search criteria author name");
                            parsedBookInfo.AuthorName = searchCriteria.Author.Name;
                        }

                        if (parsedBookInfo.BookTitle.IsNullOrWhiteSpace() && doiMatchForParsedInfo)
                        {
                            if (searchCriteria.Books != null && searchCriteria.Books.Any())
                            {
                                _logger.Debug("Book title empty but DOI matches, using search criteria book title");
                                parsedBookInfo.BookTitle = searchCriteria.Books.First().Title;
                            }
                            else if (searchCriteria is IndexerSearch.Definitions.BookSearchCriteria bookCriteria &&
                                     bookCriteria.BookTitle.IsNotNullOrWhiteSpace())
                            {
                                _logger.Debug("Book title empty but DOI matches, using search criteria book title");
                                parsedBookInfo.BookTitle = bookCriteria.BookTitle;
                            }
                        }

                        if (parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
                        {
                            var temp = new RemoteBook
                            {
                                Release = report,
                                ParsedBookInfo = parsedBookInfo
                            };

                            decision = new DownloadDecision(temp, new Rejection("Unable to parse release"));
                        }
                    }

                    // If we have parsed info (possibly filled from search criteria) but haven't mapped to a RemoteBook yet, do it now.
                    if (decision == null && remoteBook == null && parsedBookInfo != null)
                    {
                        remoteBook = _parsingService.Map(parsedBookInfo, searchCriteria);
                        remoteBook.Release = report;
                    }

                    // If we have a remoteBook and still no decision, run the standard evaluation pipeline.
                    if (decision == null && remoteBook != null)
                    {
                        // Only fill author/books from search criteria if DOI matches (DOI is a strong identifier)
                        var doiMatchForFallback = DoiUtility.IsDoiMatch(report, searchCriteria);

                        if (remoteBook.Author == null && searchCriteria?.Author != null && doiMatchForFallback)
                        {
                            _logger.Debug("Author not parsed but DOI matches, using search criteria author");
                            remoteBook.Author = searchCriteria.Author;
                        }

                        if (remoteBook.Books.Empty() && searchCriteria?.Books != null && doiMatchForFallback)
                        {
                            _logger.Debug("Books not parsed but DOI matches, using search criteria books");
                            remoteBook.Books = searchCriteria.Books;
                        }

                        if (remoteBook.ParsedBookInfo?.Quality?.Quality == Quality.Unknown)
                        {
                            remoteBook.ParsedBookInfo.Quality = new QualityModel(Quality.PDF);
                        }

                        if (remoteBook.Author == null)
                        {
                            decision = new DownloadDecision(remoteBook, new Rejection("Unknown Author"));
                        }
                        else if (remoteBook.Books.Empty())
                        {
                            decision = new DownloadDecision(remoteBook, new Rejection("Unable to parse books from release name"));
                        }
                        else
                        {
                            _aggregationService.Augment(remoteBook);

                            remoteBook.CustomFormats = _formatCalculator.ParseCustomFormat(remoteBook, remoteBook.Release.Size);
                            remoteBook.CustomFormatScore = remoteBook?.Author?.QualityProfile?.Value.CalculateCustomFormatScore(remoteBook.CustomFormats) ?? 0;

                            remoteBook.DownloadAllowed = remoteBook.Books.Any();
                            decision = GetDecisionForReport(remoteBook, searchCriteria);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var errorRemote = new RemoteBook { Release = report };
                    decision = new DownloadDecision(errorRemote, new Rejection("Unexpected error processing release"));
                }

                reportNumber++;

                if (decision != null)
                {
                    var source = pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;

                    if (searchCriteria != null)
                    {
                        if (searchCriteria.InteractiveSearch)
                        {
                            source = ReleaseSourceType.InteractiveSearch;
                        }
                        else if (searchCriteria.UserInvokedSearch)
                        {
                            source = ReleaseSourceType.UserInvokedSearch;
                        }
                        else
                        {
                            source = ReleaseSourceType.Search;
                        }
                    }

                    decision.RemoteBook.ReleaseSource = source;

                    if (decision.Rejections.Any())
                    {
                        _logger.Debug("Release rejected for the following reasons: {0}", string.Join(", ", decision.Rejections));
                    }
                    else
                    {
                        _logger.Debug("Release accepted");
                    }

                    yield return decision;
                }
            }
        }

        private DownloadDecision GetDecisionForReport(RemoteBook remoteBook, SearchCriteriaBase searchCriteria = null)
        {
            var reasons = new Rejection[0];

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteBook, searchCriteria))
                                                        .Where(c => c != null)
                                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteBook, reasons.ToArray());
        }

        private Rejection EvaluateSpec(IDecisionEngineSpecification spec, RemoteBook remoteBook, SearchCriteriaBase searchCriteriaBase = null)
        {
            try
            {
                var result = spec.IsSatisfiedBy(remoteBook, searchCriteriaBase);

                if (!result.Accepted)
                {
                    return new Rejection(result.Reason, spec.Type);
                }
            }
            catch (NotImplementedException)
            {
                _logger.Trace("Spec " + spec.GetType().Name + " not implemented.");
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteBook.Release.ToJson());
                e.Data.Add("parsed", remoteBook.ParsedBookInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}", remoteBook.Release.Title);
                return new Rejection($"{spec.GetType().Name}: {e.Message}");
            }

            return null;
        }
    }
}
