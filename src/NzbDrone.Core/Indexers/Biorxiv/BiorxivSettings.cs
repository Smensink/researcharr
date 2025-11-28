using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Biorxiv
{
    public interface IBiorxivSettings : IIndexerSettings
    {
    }

    public abstract class BiorxivSettingsBase : IBiorxivSettings
    {
        private static readonly BiorxivSettingsValidator Validator = new BiorxivSettingsValidator();

        [FieldDefinition(0, Label = "Base URL", HelpText = "Base API URL for the preprint server")]
        public string BaseUrl { get; set; } = "https://api.biorxiv.org/details";

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class BiorxivSettings : BiorxivSettingsBase
    {
    }

    public class MedrxivSettings : BiorxivSettingsBase
    {
    }

    public class BiorxivSettingsValidator : AbstractValidator<BiorxivSettingsBase>
    {
        public BiorxivSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).IsValidUrl();
        }
    }
}
