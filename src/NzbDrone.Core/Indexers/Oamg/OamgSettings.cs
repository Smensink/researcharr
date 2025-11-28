using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Oamg
{
    public class OamgSettings : IIndexerSettings
    {
        private static readonly OamgSettingsValidator Validator = new OamgSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for OA.mg/OpenAlex API")]
        public string BaseUrl { get; set; } = "https://api.openalex.org";

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class OamgSettingsValidator : AbstractValidator<OamgSettings>
    {
        public OamgSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
