using System.Linq;
using NLog;
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
                    _logger.Debug("Books not parsed but DOI matches, accepting release: {0}", remoteBook.ParsedBookInfo);
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
                _logger.Debug("Release rejected since the book wasn't requested: {0}", remoteBook.ParsedBookInfo);
                return Decision.Reject("Book wasn't requested");
            }

            return Decision.Accept();
        }
    }
}
