using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class AuthorSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public AuthorSpecification(Logger logger)
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

            if (searchCriteria.Author == null)
            {
                return Decision.Accept();
            }

            _logger.Debug("Checking if author/journal matches searched author/journal");

            var remoteAuthor = remoteBook.Author;
            var searchAuthor = searchCriteria.Author;

            // Primary match: Check if both are journals and they match
            var remoteIsJournal = remoteAuthor?.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                                  string.Equals(remoteAuthor?.Metadata?.Value?.Disambiguation, "Journal", System.StringComparison.InvariantCultureIgnoreCase);
            var searchIsJournal = searchAuthor?.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                                  string.Equals(searchAuthor?.Metadata?.Value?.Disambiguation, "Journal", System.StringComparison.InvariantCultureIgnoreCase);

            if (remoteIsJournal && searchIsJournal)
            {
                // Both are journals - must match exactly
                if (remoteAuthor != null && (remoteAuthor.Id == searchAuthor.Id || 
                    remoteAuthor?.Metadata?.Value?.ForeignAuthorId == searchAuthor?.Metadata?.Value?.ForeignAuthorId))
                {
                    return Decision.Accept();
                }

                _logger.Debug("Journal {0} does not match {1}", remoteAuthor, searchAuthor);
                return Decision.Reject("Wrong journal");
            }

            // If search criteria has a journal but remote book has a person author (shouldn't happen with new system, but handle for compatibility)
            if (searchIsJournal && !remoteIsJournal)
            {
                _logger.Debug("Search criteria specifies journal {0} but remote book has person author {1}", searchAuthor, remoteAuthor);
                return Decision.Reject("Wrong author type - expected journal");
            }

            // If remote book has a journal but search criteria has a person author, check parsed author names as secondary filter
            if (remoteIsJournal && !searchIsJournal)
            {
                // Check if parsed author name from remote book matches search criteria author name
                var parsedAuthorName = remoteBook.ParsedBookInfo?.AuthorName;
                if (parsedAuthorName.IsNotNullOrWhiteSpace() && searchAuthor?.Metadata?.Value?.Name.IsNotNullOrWhiteSpace() == true)
                {
                    var parsedClean = Parser.Parser.CleanAuthorName(parsedAuthorName);
                    var searchClean = Parser.Parser.CleanAuthorName(searchAuthor.Metadata.Value.Name);
                    
                    if (parsedClean.Equals(searchClean, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Debug("Journal {0} matches search criteria journal, and parsed author name '{1}' matches search author '{2}'", 
                            remoteAuthor, parsedAuthorName, searchAuthor.Metadata.Value.Name);
                        return Decision.Accept();
                    }
                }

                // If we have a journal but parsed author names don't match, still accept if journal matches
                // (papers can have multiple authors, so we don't require exact author match)
                _logger.Debug("Remote book has journal {0}, search criteria has person author {1}. Accepting based on journal match.", 
                    remoteAuthor, searchAuthor);
                return Decision.Accept();
            }

            // Both are person authors (legacy case) - must match exactly
            if (remoteAuthor == null)
            {
                _logger.Debug("Remote book has no author but search criteria requires author {0}", searchAuthor);
                return Decision.Reject("Remote book has no author");
            }

            if (remoteAuthor.Id == searchAuthor.Id)
            {
                return Decision.Accept();
            }

            _logger.Debug("Author {0} does not match {1}", remoteAuthor, searchAuthor);
            return Decision.Reject("Wrong author");
        }
    }
}
