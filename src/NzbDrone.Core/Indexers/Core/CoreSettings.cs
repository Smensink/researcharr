using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Core
{
    public class CoreSettings : IIndexerSettings
    {
        private static readonly CoreSettingsValidator Validator = new CoreSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for CORE API")]
        public string BaseUrl { get; set; } = "https://api.core.ac.uk/v3";

        [FieldDefinition(1, Label = "API Key", HelpText = "Your CORE API Key (optional but recommended)", Type = FieldType.Textbox)]
        public string ApiKey { get; set; }

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class CoreSettingsValidator : AbstractValidator<CoreSettings>
    {
        public CoreSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
