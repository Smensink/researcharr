using System.Linq;
using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
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

            // If remote book has no books parsed, reject for interactive search
            if (remoteBook.Books == null || !remoteBook.Books.Any())
            {
                if (searchCriteria.InteractiveSearch)
                {
                    _logger.Debug("Release rejected since no books could be parsed: {0}", remoteBook.ParsedBookInfo);
                    return Decision.Reject("Unable to parse books from release name");
                }

                return Decision.Accept();
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
