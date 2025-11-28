using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeDiskSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeDiskSpecification(UpgradableSpecification qualityUpgradableSpecification,
                                        ICacheManager cacheManager,
                                        ICustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _upgradableSpecification = qualityUpgradableSpecification;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            if (subject.Books == null || !subject.Books.Any() || subject.Author?.QualityProfile == null)
            {
                return Decision.Reject("Unable to evaluate disk upgrade without author/books");
            }

            if (subject.ParsedBookInfo?.Quality == null)
            {
                return Decision.Reject("Unable to evaluate disk upgrade without parsed quality");
            }

            var bookFiles = subject.Books
                .Where(c => c?.BookFiles != null)
                .SelectMany(c => c.BookFiles?.Value ?? Enumerable.Empty<BookFile>())
                .Where(f => f != null);

            foreach (var file in bookFiles)
            {
                var customFormats = _formatService.ParseCustomFormat(file);

                if (!_upgradableSpecification.IsUpgradable(subject.Author.QualityProfile,
                                                           file.Quality,
                                                           customFormats,
                                                           subject.ParsedBookInfo.Quality,
                                                           subject.CustomFormats))
                {
                    return Decision.Reject("Existing files on disk is of equal or higher preference: {0}", file.Quality.Quality.Name);
                }
            }

            return Decision.Accept();
        }
    }
}
