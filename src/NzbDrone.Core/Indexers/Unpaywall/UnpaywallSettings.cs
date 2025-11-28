using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Unpaywall
{
    public class UnpaywallSettings : IIndexerSettings
    {
        private static readonly UnpaywallSettingsValidator Validator = new UnpaywallSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for Unpaywall API")]
        public string BaseUrl { get; set; } = "https://api.unpaywall.org/v2";

        [FieldDefinition(1, Label = "Email", HelpText = "Required for Unpaywall API usage", Type = FieldType.Textbox)]
        public string Email { get; set; }

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class UnpaywallSettingsValidator : AbstractValidator<UnpaywallSettings>
    {
        public UnpaywallSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
            RuleFor(c => c.Email).NotEmpty().EmailAddress();
        }
    }
}
