using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.MetadataSource.OpenAlex
{
    public class OpenAlexSettings : IProviderConfig
    {
        private static readonly OpenAlexSettingsValidator Validator = new OpenAlexSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for the OpenAlex API")]
        public string BaseUrl { get; set; } = "https://api.openalex.org";

        [FieldDefinition(1, Label = "User Agent / Email", HelpText = "Email to use in User-Agent for the polite pool")]
        public string UserAgent { get; set; } = "Researcharr/1.0 (mailto:researcharr@example.com)";

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class OpenAlexSettingsValidator : AbstractValidator<OpenAlexSettings>
    {
        public OpenAlexSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
            RuleFor(c => c.UserAgent).NotEmpty();
        }
    }
}
