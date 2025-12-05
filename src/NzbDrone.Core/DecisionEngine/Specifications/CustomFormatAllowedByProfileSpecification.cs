using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CustomFormatAllowedbyProfileSpecification : IDecisionEngineSpecification
    {
        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            if (subject == null)
            {
                return Decision.Accept();
            }

            var minScore = subject.Author?.QualityProfile?.Value?.MinFormatScore;
            var score = subject.CustomFormatScore;

            if (!minScore.HasValue)
            {
                return Decision.Accept();
            }

            if (score < minScore.Value)
            {
                var formats = subject.CustomFormats?.ConcatToString() ?? string.Empty;

                return Decision.Reject("Custom Formats {0} have score {1} below Author profile minimum {2}", formats, score, minScore.Value);
            }

            return Decision.Accept();
        }
    }
}
