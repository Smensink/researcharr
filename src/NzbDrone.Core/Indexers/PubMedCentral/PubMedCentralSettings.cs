using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.PubMedCentral
{
    public class PubMedCentralSettings : IIndexerSettings
    {
        private static readonly PubMedCentralSettingsValidator Validator = new PubMedCentralSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for PubMed Central E-utilities API")]
        public string BaseUrl { get; set; } = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils";

        [FieldDefinition(1, Label = "API Key", HelpText = "Optional NCBI API key for higher rate limits (10 requests/sec vs 3/sec)", Type = FieldType.Textbox, Advanced = true)]
        public string ApiKey { get; set; }

        [FieldDefinition(2, Label = "Email", HelpText = "Optional email for NCBI tracking (recommended)", Type = FieldType.Textbox, Advanced = true)]
        public string Email { get; set; }

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class PubMedCentralSettingsValidator : AbstractValidator<PubMedCentralSettings>
    {
        public PubMedCentralSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
