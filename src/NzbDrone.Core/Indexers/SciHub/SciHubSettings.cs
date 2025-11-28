using FluentValidation;
using Newtonsoft.Json;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.SciHub
{
    public class SciHubSettings : IIndexerSettings
    {
        private static readonly SciHubSettingsValidator Validator = new SciHubSettingsValidator();

        [FieldDefinition(0, Label = "Mirrors", HelpText = "List of Sci-Hub mirrors (comma or newline separated), e.g. https://sci-hub.wf", Type = FieldType.Textbox)]
        public string Mirrors { get; set; } =
            "https://sci-hub.wf, https://sci-hub.st, https://sci-hub.se, https://sci-hub.ru, https://sci-hub.ee, https://sci-hub.ren";

        [JsonIgnore]
        public string BaseUrl { get; set; } = "https://sci-hub.wf";

        [FieldDefinition(2, Label = "FlareSolverr URL", HelpText = "Optional. If set, Sci-Hub requests will be routed via FlareSolverr (e.g. http://flaresolverr:8191)", Type = FieldType.Textbox, Advanced = true)]
        public string FlareSolverrUrl { get; set; }

        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public class SciHubSettingsValidator : AbstractValidator<SciHubSettings>
    {
        public SciHubSettingsValidator()
        {
            RuleFor(c => c.Mirrors).NotEmpty();
        }
    }
}
