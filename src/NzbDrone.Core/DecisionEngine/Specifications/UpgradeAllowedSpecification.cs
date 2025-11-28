using System.Linq;
using NLog;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeAllowedSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeAllowedSpecification(UpgradableSpecification upgradableSpecification,
                                           Logger logger,
                                           ICustomFormatCalculationService formatService)
        {
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            if (subject.Author?.QualityProfile == null || subject.Books == null || !subject.Books.Any())
            {
                return Decision.Reject("Unable to evaluate upgrade allowance without author/books");
            }

            if (subject.ParsedBookInfo?.Quality == null)
            {
                return Decision.Reject("Unable to evaluate upgrade allowance without parsed quality");
            }

            var qualityProfile = subject.Author.QualityProfile.Value;

            var bookFiles = subject.Books
                .Where(b => b?.BookFiles != null)
                .SelectMany(b => b.BookFiles?.Value ?? Enumerable.Empty<BookFile>())
                .Where(f => f != null);

            foreach (var file in bookFiles)
            {
                var fileCustomFormats = _formatService.ParseCustomFormat(file, subject.Author);
                _logger.Debug("Comparing file quality with report. Existing files contain {0}", file.Quality);

                if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                               file.Quality,
                                                               fileCustomFormats,
                                                               subject.ParsedBookInfo.Quality,
                                                               subject.CustomFormats))
                {
                    _logger.Debug("Upgrading is not allowed by the quality profile");

                    return Decision.Reject("Existing files and the Quality profile does not allow upgrades");
                }
            }

            return Decision.Accept();
        }
    }
}
