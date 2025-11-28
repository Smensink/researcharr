using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Arxiv
{
    public class ArxivSettings : IIndexerSettings
    {
        private static readonly ArxivSettingsValidator Validator = new ArxivSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "The base URL for Arxiv API")]
        public string BaseUrl { get; set; } = "https://export.arxiv.org/api";

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class ArxivSettingsValidator : AbstractValidator<ArxivSettings>
    {
        public ArxivSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
