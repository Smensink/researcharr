using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class QualityAllowedByProfileSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public QualityAllowedByProfileSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            if (subject == null)
            {
                return Decision.Accept();
            }

            var parsedQuality = subject.ParsedBookInfo?.Quality;
            var profile = subject.Author?.QualityProfile?.Value;

            if (parsedQuality?.Quality == null || profile?.Items == null)
            {
                return Decision.Accept();
            }

            _logger.Debug("Checking if report meets quality requirements. {0}", parsedQuality);

            var qualityIndex = profile.GetIndex(parsedQuality.Quality);

            if (qualityIndex.Index < 0 || qualityIndex.Index >= profile.Items.Count)
            {
                return Decision.Accept();
            }

            var qualityOrGroup = profile.Items[qualityIndex.Index];

            if (!qualityOrGroup.Allowed)
            {
                _logger.Debug("Quality {0} rejected by Author's quality profile", parsedQuality);
                return Decision.Reject("{0} is not wanted in profile", parsedQuality.Quality);
            }

            return Decision.Accept();
        }
    }
}
