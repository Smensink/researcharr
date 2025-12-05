using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class BookRequestedSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public BookRequestedSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public Decision IsSatisfiedBy(RemoteBook remoteBook, SearchCriteriaBase searchCriteria)
        {
            if (searchCriteria == null)
            {
                return Decision.Accept();
            }

            // If no books in search criteria, accept (e.g., author search)
            if (searchCriteria.Books == null || !searchCriteria.Books.Any())
            {
                return Decision.Accept();
            }

            // If remote book has no books parsed, check if we can trust it via DOI match
            if (remoteBook.Books == null || !remoteBook.Books.Any())
            {
                // For interactive searches, always reject if books can't be parsed
                if (searchCriteria.InteractiveSearch)
                {
                    _logger.Debug("Release rejected since no books could be parsed: {0}", remoteBook.ParsedBookInfo);
                    return Decision.Reject("Unable to parse books from release name");
                }

                // For automatic searches, only accept if DOI matches (DOI is a strong identifier)
                // This prevents wrong papers from being downloaded when parsing fails
                if (remoteBook.Release != null && DoiUtility.IsDoiMatch(remoteBook.Release, searchCriteria))
                {
                    // Even with DOI match, verify the parsed title (if available) is somewhat similar to search criteria
                    // This adds an extra safety check to prevent completely unrelated papers
                    if (remoteBook.ParsedBookInfo?.BookTitle != null && searchCriteria.Books.Any())
                    {
                        var parsedTitle = Parser.Parser.NormalizeTitleSeparators(remoteBook.ParsedBookInfo.BookTitle);
                        var searchTitles = searchCriteria.Books.Select(b => Parser.Parser.NormalizeTitleSeparators(b.Title)).ToList();
                        
                        // Check if parsed title has any similarity to search criteria titles
                        // Use same word delimiters as Parser class
                        var wordDelimiters = new HashSet<char>(" .,_-=()[]|\"`'’");
                        var hasSimilarity = searchTitles.Any(searchTitle =>
                        {
                            var result = parsedTitle.ToLowerInvariant().FuzzyMatch(searchTitle.ToLowerInvariant(), 0.5, wordDelimiters);
                            return result.Item3 >= 0.5; // Require at least 50% similarity even with DOI match
                        });

                        if (!hasSimilarity)
                        {
                            _logger.Debug("Release rejected: DOI matches but parsed title '{0}' is too different from search criteria titles: {1}", 
                                parsedTitle, string.Join(", ", searchTitles));
                            return Decision.Reject("Parsed title doesn't match search criteria even though DOI matches");
                        }
                    }

                    _logger.Debug("Books not parsed but DOI matches and title similarity check passed, accepting release: {0}", remoteBook.ParsedBookInfo);
                    return Decision.Accept();
                }

                // No books parsed and no DOI match - reject to prevent wrong downloads
                _logger.Debug("Release rejected: no books parsed and no DOI match: {0}", remoteBook.ParsedBookInfo);
                return Decision.Reject("Unable to parse books from release name and no DOI match");
            }

            var criteriaBook = searchCriteria.Books.Select(v => v.Id).ToList();
            var remoteBooks = remoteBook.Books.Select(v => v.Id).ToList();

            if (!criteriaBook.Intersect(remoteBooks).Any())
            {
                _logger.Debug("Release rejected since the book wasn't requested: {0}. Parsed books: [{1}], Search criteria books: [{2}]", 
                    remoteBook.ParsedBookInfo,
                    string.Join(", ", remoteBooks),
                    string.Join(", ", criteriaBook));
                return Decision.Reject("Book wasn't requested");
            }

            // Additional validation: if we have a parsed book title, verify it actually matches one of the search criteria books
            // This prevents cases where parsing matched the wrong book but GetBooks returned all search criteria books
            if (remoteBook.ParsedBookInfo?.BookTitle != null && searchCriteria.Books.Any())
            {
                var parsedTitle = Parser.Parser.NormalizeTitleSeparators(remoteBook.ParsedBookInfo.BookTitle);
                var searchTitles = searchCriteria.Books.Select(b => Parser.Parser.NormalizeTitleSeparators(b.Title)).ToList();
                
                // Check if parsed title has sufficient similarity to any search criteria title
                var wordDelimiters = new HashSet<char>(" .,_-=()[]|\"`''");
                var hasSimilarity = searchTitles.Any(searchTitle =>
                {
                    var result = parsedTitle.ToLowerInvariant().FuzzyMatch(searchTitle.ToLowerInvariant(), 0.5, wordDelimiters);
                    return result.Item3 >= 0.6; // Require at least 60% similarity for automatic searches
                });

                if (!hasSimilarity)
                {
                    _logger.Debug("Release rejected: parsed title '{0}' doesn't match search criteria titles: {1}. Book IDs matched but titles don't.", 
                        parsedTitle, string.Join(", ", searchTitles));
                    return Decision.Reject("Parsed title doesn't match search criteria");
                }
            }

            return Decision.Accept();
        }
    }
}
