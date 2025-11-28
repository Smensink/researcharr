using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Doaj
{
    public class DoajSettings : IIndexerSettings
    {
        private static readonly DoajSettingsValidator Validator = new DoajSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for DOAJ API")]
        public string BaseUrl { get; set; } = "https://doaj.org/api/v2";

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class DoajSettingsValidator : AbstractValidator<DoajSettings>
    {
        public DoajSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
